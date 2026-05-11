#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class JudgeViewModel : BaseViewModel
{
    #region Services
    private readonly IAlertService _alertService;
    private readonly IBluetoothClient _bluetoothClient;
    private readonly IBluetoothService _bluetoothService;
    private readonly IRepository<CompetitionModel> _competitionRepository;
    #endregion

    #region Attributs
    private CompetitorModel? _competitor1;
    private CompetitorModel? _competitor2;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private JudgeModel? _selectedJudge;

    [ObservableProperty]
    private string? _currentMatchInfo;

    [ObservableProperty]
    private string? _categoryName;

    [ObservableProperty]
    private string? _redName;

    [ObservableProperty]
    private string? _blueName;

    [ObservableProperty]
    private int _matchNumber;

    [ObservableProperty]
    private string _timeLeftDisplay = "02:00";

    private TimeSpan _timeLeft = TimeSpan.FromMinutes(2);
    private IDispatcherTimer? _timer;

    private string? _serverDeviceId;

    private readonly Dictionary<string, (string Type, string?[] Chunks)> _pendingChunks = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true
    };
    #endregion

    #region Properties
    public ObservableCollection<DiscoveredDeviceModel> DiscoveredDevices { get; } = new();
    public ObservableCollection<JudgeModel> JudgePositions { get; } = new();

    public CompetitorModel? Competitor1
    {
        get => _competitor1;
        set => SetProperty(ref _competitor1, value);
    }

    public CompetitorModel? Competitor2
    {
        get => _competitor2;
        set => SetProperty(ref _competitor2, value);
    }
    #endregion

    #region Constructors
    public JudgeViewModel(
        ILogger<JudgeViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IAlertService alertService,
        IBluetoothClient bluetoothClient,
        IBluetoothService bluetoothService,
        IRepository<CompetitionModel> competitionRepository) : base(logger, resourceProvider, popupService)
    {
        _alertService = alertService;

        _bluetoothClient = bluetoothClient;
        _bluetoothService = bluetoothService;
        _competitionRepository = competitionRepository;
        _title = "JUDGE";

        for (int i = 1; i <= 5; i++)
        {
            JudgePositions.Add(new JudgeModel { Name = $"JUGE {i}", Number = i });
        }

        _timer = Application.Current?.Dispatcher.CreateTimer();
        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                if (_timeLeft.TotalSeconds > 0)
                {
                    _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
                    TimeLeftDisplay = _timeLeft.ToString(@"mm\:ss");
                }
                else
                {
                    _timer.Stop();
                }
            };
        }
    }
    #endregion

    #region Event Handlers
    private void OnDeviceDiscovered(object? sender, (string DeviceId, string Name, int Rssi) device)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = DiscoveredDevices.FirstOrDefault(d => d.DeviceId == device.DeviceId);
            if (existing != null)
            {
                existing.Rssi = device.Rssi;
                existing.Name = device.Name;
            }
            else
            {
                DiscoveredDevices.Add(new DiscoveredDeviceModel 
                { 
                    DeviceId = device.DeviceId, 
                    Name = device.Name, 
                    Rssi = device.Rssi 
                });
            }
        });
    }

    private void OnDeviceConnected(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _serverDeviceId = deviceId;
            IsConnected = true;
            IsScanning = false;
        });
    }

    private void OnDeviceDisconnected(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _serverDeviceId = null;
            IsConnected = false;
            _timer?.Stop();
            _pendingChunks.Clear();
        });
    }

    private void OnMessageReceived(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await _alertService.ShowToast($"OnMessageRecevied : {message}");

                // Gestion du découpage (Chunking)
                if (message.StartsWith("CHUNK:"))
                {
                    var parts = message.Split(':', 6);
                    if (parts.Length < 6) return;

                    string id = parts[1];
                    int index = int.Parse(parts[2]);
                    int total = int.Parse(parts[3]);
                    string type = parts[4];
                    string payload = parts[5];

                    if (!_pendingChunks.ContainsKey(id))
                    {
                        _pendingChunks[id] = (type, new string?[total]);
                    }

                    var msgData = _pendingChunks[id];
                    msgData.Chunks[index] = payload;

                    if (msgData.Chunks.All(c => c != null))
                    {
                        string fullContent = string.Join("", msgData.Chunks);
                        _pendingChunks.Remove(id);
                        await ProcessMessageAsync(type, fullContent);
                    }
                    return;
                }

                if (message == "TIMER_START")
                {
                    _timer?.Start();
                    return;
                }
                
                if (message == "TIMER_PAUSE")
                {
                    _timer?.Stop();
                    return;
                }

                if (message == "TIMER_STOP")
                {
                    _timer?.Stop();
                    _timeLeft = TimeSpan.Zero;
                    TimeLeftDisplay = "00:00";
                    return;
                }

                if (message.StartsWith("TIMER_SET:"))
                {
                    var timeStr = message.Substring("TIMER_SET:".Length);
                    if (TimeSpan.TryParseExact(timeStr, @"mm\:ss", null, out var newTime))
                    {
                        _timeLeft = newTime;
                        TimeLeftDisplay = _timeLeft.ToString(@"mm\:ss");
                    }
                    return;
                }

                // Pour compatibilité ou messages simples
                if (message.StartsWith("COMPETITION_DATA:"))
                {
                    await ProcessMessageAsync("COMPETITION_DATA", message.Substring("COMPETITION_DATA:".Length));
                }
                else if (message.StartsWith("MATCH_INFO:"))
                {
                    await ProcessMessageAsync("MATCH_INFO", message.Substring("MATCH_INFO:".Length));
                }
                else
                {
                    CurrentMatchInfo = message;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received message");
                await _alertService.ShowToast($"Error: {ex.Message}");
            }
        });
    }

    private async Task ProcessMessageAsync(string type, string content)
    {
        try
        {
            if (type == "COMPETITION_DATA")
            {
                string decompressedContent = DecompressString(content);
                var competition = JsonSerializer.Deserialize<CompetitionModel>(decompressedContent, _jsonOptions);
                if (competition != null)
                {
                    var existing = await _competitionRepository.GetByIdAsync(competition.Id);
                    if (existing != null)
                        await _competitionRepository.UpdateAsync(competition);
                    else
                        await _competitionRepository.AddAsync(competition);
                    
                    await _alertService.ShowToast($"Compétition reçue : {competition.Name}");
                }
            }
            else if (type == "MATCH_INFO")
            {
                var matchData = JsonSerializer.Deserialize<MatchInfoData>(content, _jsonOptions);
                if (matchData != null)
                {
                    CategoryName = matchData.CategoryName;
                    Competitor1 = matchData.Competitor1;
                    Competitor2 = matchData.Competitor2;

                    MatchNumber = matchData.MatchNumber;
                    CurrentMatchInfo = $"{CategoryName} - Match #{MatchNumber}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing {Type}", type);
            await _alertService.ShowToast($"Erreur de données : {type}");
        }
    }

    private string DecompressString(string compressedText)
    {
        byte[] gZipBuffer = Convert.FromBase64String(compressedText);
        using var ms = new MemoryStream(gZipBuffer);
        using var zip = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(zip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private class MatchInfoData
    {
        public string? CategoryName { get; set; }
        public string? RedName { get; set; }
        public string? BlueName { get; set; }
        public int MatchNumber { get; set; }
        public CompetitorModel? Competitor1 { get; set; }
        public CompetitorModel? Competitor2 { get; set; }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task StartScan()
    {
        if (await _bluetoothService.RequestBluetoothPermissions())
        {
            DiscoveredDevices.Clear();
            IsScanning = true;
            await _bluetoothClient.StartScan();
        }
    }

    [RelayCommand]
    private async Task Connect(DiscoveredDeviceModel? device)
    {
        if (device != null)
        {
            await _bluetoothClient.StopScan();
            IsScanning = false;
            await _bluetoothClient.ConnectToDevice(device.DeviceId);
        }
    }

    [RelayCommand]
    private async Task SelectPosition(JudgeModel judge)
    {
        SelectedJudge = judge;
        if (IsConnected && _serverDeviceId != null)
        {
            await _bluetoothClient.SendMessage(_serverDeviceId, $"JUDGE_POSITION:{judge.Number}");
        }
    }
    #endregion

    #region Override Methods
    public override Task OnAppearing()
    {
        var clientId = _bluetoothClient.InstanceId.ToString().Substring(0, 8);
        _alertService.ShowToast($"Judge VM OnAppearing - ClientID:[{clientId}]");

        _bluetoothClient.DeviceDiscovered += OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected += OnDeviceConnected;
        _bluetoothClient.DeviceDisconnected += OnDeviceDisconnected;
        _bluetoothClient.MessageReceived += OnMessageReceived;
        return base.OnAppearing();
    }

    public override Task OnDisappearing()
    {
        _bluetoothClient.DeviceDiscovered -= OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected -= OnDeviceConnected;
        _bluetoothClient.DeviceDisconnected -= OnDeviceDisconnected;
        _bluetoothClient.MessageReceived -= OnMessageReceived;
        return base.OnDisappearing();
    }
    #endregion
}
