#region Imports
using CommunityToolkit.Maui;
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
    private readonly IRepository<DrawKnockoutModel> _drawKnockoutService;
    private readonly IRepository<DrawOrderModel> _drawOrderService;
    private readonly IRepository<DrawPoolsModel> _drawPoolsService;
    #endregion

    #region Attributs
    private CompetitorModel? _competitor1;
    private CompetitorModel? _competitor2;

    private string _title;
    private bool _isScanning;
    private bool _isConnected;
    private bool _isReceivingCompetition;
    private bool _isReceivingMatch;
    private JudgeModel? _selectedJudge;
    private string? _currentMatchInfo;
    private string? _categoryName;
    private int _matchNumber;
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

    private IDrawModel? _currentDraw;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public bool IsReceivingCompetition
    {
        get => _isReceivingCompetition;
        set => SetProperty(ref _isReceivingCompetition, value);
    }

    public bool IsReceivingMatch
    {
        get => _isReceivingMatch;
        set => SetProperty(ref _isReceivingMatch, value);
    }

    public JudgeModel? SelectedJudge
    {
        get => _selectedJudge;
        set => SetProperty(ref _selectedJudge, value);
    }

    public string? CurrentMatchInfo
    {
        get => _currentMatchInfo;
        set => SetProperty(ref _currentMatchInfo, value);
    }

    public string? CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public int MatchNumber
    {
        get => _matchNumber;
        set => SetProperty(ref _matchNumber, value);
    }

    public string TimeLeftDisplay
    {
        get => _timeLeftDisplay;
        set => SetProperty(ref _timeLeftDisplay, value);
    }

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

    public IDrawModel? CurrentDraw
    {
        get => _currentDraw;
        set => SetProperty(ref _currentDraw, value);
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
        IRepository<CompetitionModel> competitionRepository,
        IRepository<DrawKnockoutModel> drawKnockoutService,
        IRepository<DrawOrderModel> drawOrderService,
        IRepository<DrawPoolsModel> drawPoolsService
    ) : base(logger, resourceProvider, popupService)
    {
        _alertService = alertService;

        _bluetoothClient = bluetoothClient;
        _bluetoothService = bluetoothService;

        _competitionRepository = competitionRepository;
        _drawKnockoutService = drawKnockoutService;
        _drawOrderService = drawOrderService;
        _drawPoolsService = drawPoolsService;

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
                if (message == Constants.Message.TIMER_START)
                {
                    _timer?.Start();
                    return;
                }

                if (message == Constants.Message.TIMER_PAUSE)
                {
                    _timer?.Stop();
                    return;
                }

                if (message == Constants.Message.TIMER_STOP)
                {
                    _timer?.Stop();
                    _timeLeft = TimeSpan.Zero;
                    TimeLeftDisplay = "00:00";
                    return;
                }

                if (message.StartsWith(Constants.Message.TIMER_SET))
                {
                    var timeStr = message.Substring(Constants.Message.TIMER_SET.Length);
                    if (TimeSpan.TryParseExact(timeStr, @"mm\:ss", null, out var newTime))
                    {
                        _timeLeft = newTime;
                        TimeLeftDisplay = _timeLeft.ToString(@"mm\:ss");
                    }
                    return;
                }

                if (message.StartsWith(Constants.Message.COMPETITION_DATA))
                {
                    var content = message.Substring(Constants.Message.COMPETITION_DATA.Length);
                    var competition = JsonSerializer.Deserialize<CompetitionModel>(content, _jsonOptions);
                    if (competition != null)
                    {
                        // Clean the graph to avoid EF Core key/seed conflicts
                        CleanCompetitionGraph(competition);

                        var existing = await _competitionRepository.GetByIdAsync(competition.Id);
                        if (existing != null)
                            await _competitionRepository.UpdateAsync(competition);
                        else
                            await _competitionRepository.AddAsync(competition);

                        await _alertService.ShowToast($"Compétition reçue : {competition.Name}");
                    }
                    IsReceivingCompetition = false;
                    IsReceivingMatch = true;
                }
                else if (message.StartsWith(Constants.Message.MATCH_INFO))
                {
                    var content = message.Substring(Constants.Message.MATCH_INFO.Length);
                    var matchData = JsonSerializer.Deserialize<MatchInfoData>(content, _jsonOptions);

                    if (matchData != null)
                    {
                        switch (matchData.Type)
                        {
                            case RoundType.Knockouts:
                                CurrentDraw = await _drawKnockoutService.GetByIdAsync(matchData.Id, "Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser");
                                break;
                            case RoundType.Pools:
                                CurrentDraw = await _drawPoolsService.GetByIdAsync(matchData.Id, "Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser");
                                break;
                            case RoundType.Order:
                                CurrentDraw = await _drawOrderService.GetByIdAsync(matchData.Id, "Draw.Category.AgeRange", "Competitor.Country");
                                break;
                        }

                        CategoryName = CurrentDraw?.Draw?.Category?.Name;
                    }
                    IsReceivingMatch = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received message");
                await _alertService.ShowToast($"Error: {ex.Message}");
            }
        });
    }
    #endregion

    #region Private Methods
    private void CleanCompetitionGraph(CompetitionModel competition)
    {
        if (competition.Categories == null) return;

        var competitorCache = new Dictionary<int, CompetitorModel>();
        var categoryCache = new Dictionary<int, CategoryModel>();

        // 1. First pass: Collect and deduplicate all categories and competitors
        foreach (var category in competition.Categories.ToList())
        {
            if (category.Id > 0)
            {
                if (categoryCache.TryGetValue(category.Id, out var existingCat))
                {
                    // If we already have this category instance, use it and skip
                    int idx = competition.Categories.IndexOf(category);
                    competition.Categories[idx] = existingCat;
                    continue;
                }
                categoryCache[category.Id] = category;
            }

            category.Competition = null;
            category.AgeRange = null; // Let EF use AgeRangeId

            if (category.Competitors != null)
            {
                foreach (var compCat in category.Competitors)
                {
                    compCat.Category = null;
                    if (compCat.Competitor != null && compCat.Competitor.Id > 0)
                    {
                        if (competitorCache.TryGetValue(compCat.Competitor.Id, out var existingComp))
                        {
                            compCat.Competitor = existingComp;
                        }
                        else
                        {
                            competitorCache[compCat.Competitor.Id] = compCat.Competitor;
                            compCat.Competitor.Country = null; // Let EF use CountryIsoCode
                            compCat.Competitor.Categories = null;
                        }
                    }
                }
            }

            // Handle Draw and its competitors
            if (category.Draw != null)
            {
                category.Draw.Category = null;

                void CleanDrawCompetitor(CompetitorModel? competitor, Action<CompetitorModel?> setter)
                {
                    if (competitor != null && competitor.Id > 0)
                    {
                        if (competitorCache.TryGetValue(competitor.Id, out var existing))
                        {
                            setter(existing);
                        }
                        else
                        {
                            competitorCache[competitor.Id] = competitor;
                            competitor.Country = null;
                            competitor.Categories = null;
                        }
                    }
                }

                if (category.Draw.DrawKnockouts != null)
                {
                    foreach (var k in category.Draw.DrawKnockouts)
                    {
                        k.Draw = null;
                        CleanDrawCompetitor(k.Competitor1, c => k.Competitor1 = c);
                        CleanDrawCompetitor(k.Competitor2, c => k.Competitor2 = c);
                        CleanDrawCompetitor(k.Winner, c => k.Winner = c);
                        CleanDrawCompetitor(k.Looser, c => k.Looser = c);
                    }
                }

                if (category.Draw.DrawPools != null)
                {
                    foreach (var p in category.Draw.DrawPools)
                    {
                        p.Draw = null;
                        CleanDrawCompetitor(p.Competitor1, c => p.Competitor1 = c);
                        CleanDrawCompetitor(p.Competitor2, c => p.Competitor2 = c);
                        CleanDrawCompetitor(p.Winner, c => p.Winner = c);
                        CleanDrawCompetitor(p.Looser, c => p.Looser = c);
                    }
                }

                if (category.Draw.DrawOrders != null)
                {
                    foreach (var o in category.Draw.DrawOrders)
                    {
                        //o.Draw = null;
                        CleanDrawCompetitor(o.Competitor, c => o.Competitor = c);
                    }
                }
            }
        }
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
            IsReceivingCompetition = true;

            await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_POSITION}{judge.Number}");
        }
    }

    [RelayCommand]
    private async Task AddScore(int competitorId)
    {
        var json = JsonSerializer.Serialize(new TransfertScoreModel() { Score = 1, CompetitorId = competitorId }, _jsonOptions);

        if (IsConnected && _serverDeviceId != null)
        {
            await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
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

    public override async Task OnDisappearing()
    {
        if (IsConnected && _serverDeviceId != null)
        {
            await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_DISCONNECT}");
        }

        _bluetoothClient.DeviceDiscovered -= OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected -= OnDeviceConnected;
        _bluetoothClient.DeviceDisconnected -= OnDeviceDisconnected;
        _bluetoothClient.MessageReceived -= OnMessageReceived;

        await base.OnDisappearing();
    }
    #endregion
}
