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

    private string? _serverDeviceId;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true
    };
    #endregion

    #region Properties
    public ObservableCollection<DiscoveredDeviceModel> DiscoveredDevices { get; } = new();
    public ObservableCollection<JudgeModel> JudgePositions { get; } = new();
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
        });
    }

    private async void OnMessageReceived(object? sender, string message)
    {
        await _alertService.ShowToast($"OnMessageReceived => {message}");

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (message.StartsWith("COMPETITION_DATA:"))
            {
                var json = message.Substring("COMPETITION_DATA:".Length);
                try
                {
                    var competition = JsonSerializer.Deserialize<CompetitionModel>(json, _jsonOptions);
                    if (competition != null)
                    {
                        // Check if competition already exists
                        var existing = await _competitionRepository.GetByIdAsync(competition.Id);
                        if (existing != null)
                        {
                            await _competitionRepository.UpdateAsync(competition);
                        }
                        else
                        {
                            await _competitionRepository.AddAsync(competition);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing competition data");
                }
            }
            else if (message.StartsWith("MATCH_INFO:"))
            {
                var json = message.Substring("MATCH_INFO:".Length);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    CategoryName = root.GetProperty("categoryName").GetString();
                    RedName = root.GetProperty("redName").GetString();
                    BlueName = root.GetProperty("blueName").GetString();
                    MatchNumber = root.GetProperty("matchNumber").GetInt32();
                    
                    CurrentMatchInfo = $"{CategoryName} - Match #{MatchNumber}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing match info");
                }
            }
            else
            {
                CurrentMatchInfo = message;
            }
        });
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
