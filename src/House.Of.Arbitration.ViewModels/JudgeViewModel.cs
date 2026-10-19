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
    private readonly IRepository<MvtCodeModel> _mvtCodesService;
    #endregion

    #region Attributs
    private CompetitorModel? _competitor1;
    private CompetitorModel? _competitor2;

    private bool _isScanning;
    private bool _isConnected;
    private bool _isReceivingCompetition;
    private bool _isReceivingMatch;
    private JudgeModel? _selectedJudge;
    private string? _currentMatchInfo;
    private string? _categoryName;
    private int _matchNumber;
    private string _timeLeftDisplay = "02:00";

    private double _score = 7.0;

    private string _code = "";
    private CancellationTokenSource? _typeNumberCts;

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
    private CompetitionModel? _currentCompetition;
    private CompetitionType _competitionType = CompetitionType.None;
    private string _selectedRecognizedGroup = string.Empty;
    private string _searchRecognizedText = string.Empty;
    private TaoluInputMode _currentTaoluInputMode = TaoluInputMode.Code;
    private string _pointsInput = string.Empty;
    private int _lastMatchId;
    private string? _lastServerDeviceId;
    private bool _hasUnsyncedChanges;
    private readonly List<TransfertScoreModel> _unsyncedScores = new();
    private bool _isReconnecting;
    private IDispatcherTimer? _autoReconnectTimer;
    private IDispatcherTimer? _watchdogTimer;
    private DateTime _lastServerContactTime = DateTime.UtcNow;
    private int _consecutiveFailedPings;
    #endregion

    #region Properties
    public CompetitionModel? CurrentCompetition
    {
        get => _currentCompetition;
        set
        {
            if (SetProperty(ref _currentCompetition, value))
            {
                OnPropertyChanged(nameof(CanSelectPosition));
                OnPropertyChanged(nameof(ShowPositionScreen));
            }
        }
    }

    public CompetitionType CompetitionType
    {
        get => _competitionType;
        set
        {
            if (SetProperty(ref _competitionType, value))
            {
                OnPropertyChanged(nameof(IsTaolu));
                OnPropertyChanged(nameof(IsSanda));
            }
        }
    }

    public bool IsTaolu => CompetitionType == CompetitionType.Taolu;
    public bool IsSanda => CompetitionType == CompetitionType.Sanda;

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(CanSelectPosition));
                OnPropertyChanged(nameof(ShowConnectionScreen));
                OnPropertyChanged(nameof(ShowPositionScreen));
                OnPropertyChanged(nameof(IsJudging));
                OnPropertyChanged(nameof(IsServerOffline));
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(ConnectionStatusColor));
            }
        }
    }

    public bool HasUnsyncedChanges
    {
        get => _hasUnsyncedChanges;
        set
        {
            if (SetProperty(ref _hasUnsyncedChanges, value))
            {
                OnPropertyChanged(nameof(OfflineSyncStatusText));
            }
        }
    }

    public IReadOnlyList<TransfertScoreModel> UnsyncedScores => _unsyncedScores.AsReadOnly();

    public bool IsReconnecting
    {
        get => _isReconnecting;
        set
        {
            if (SetProperty(ref _isReconnecting, value))
            {
                OnPropertyChanged(nameof(ReconnectButtonText));
            }
        }
    }

    public bool ShowConnectionScreen => SelectedJudge == null && !IsConnected;

    public bool ShowPositionScreen => CanSelectPosition;

    public bool IsJudging => SelectedJudge != null;

    public bool IsServerOffline => SelectedJudge != null && !IsConnected;

    public string ConnectionStatusText => IsConnected ? "CONNECTÉ" : "DÉCONNECTÉ (HORS-LIGNE)";

    public string ConnectionStatusColor => IsConnected ? "#4CAF50" : "#FF5252";

    public string OfflineSyncStatusText => HasUnsyncedChanges
        ? "⚠️ Notations locales non synchronisées. En attente de reconnexion..."
        : "Vos notations sont conservées localement et seront synchronisées dès reconnexion.";

    public string ReconnectButtonText => IsReconnecting ? "Connexion..." : "Reconnecter";

    public DateTime LastServerContactTime
    {
        get => _lastServerContactTime;
        set => _lastServerContactTime = value;
    }

    public int ConsecutiveFailedPings
    {
        get => _consecutiveFailedPings;
        set => _consecutiveFailedPings = value;
    }

    public bool IsReceivingCompetition
    {
        get => _isReceivingCompetition;
        set
        {
            if (SetProperty(ref _isReceivingCompetition, value))
            {
                OnPropertyChanged(nameof(CanSelectPosition));
                OnPropertyChanged(nameof(ShowPositionScreen));
            }
        }
    }

    public bool IsReceivingMatch
    {
        get => _isReceivingMatch;
        set => SetProperty(ref _isReceivingMatch, value);
    }

    public JudgeModel? SelectedJudge
    {
        get => _selectedJudge;
        set
        {
            if (SetProperty(ref _selectedJudge, value))
            {
                OnPropertyChanged(nameof(CanSelectPosition));
                OnPropertyChanged(nameof(ShowConnectionScreen));
                OnPropertyChanged(nameof(ShowPositionScreen));
                OnPropertyChanged(nameof(IsJudging));
                OnPropertyChanged(nameof(IsServerOffline));
                OnPropertyChanged(nameof(GroupCodesButtonText));
                UpdateRecognizedGroups();
            }
        }
    }

    public bool CanSelectPosition => IsConnected && !IsReceivingCompetition && CurrentCompetition != null && SelectedJudge == null;

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
    public ObservableCollection<MvtTimeCodeModel> Codes { get; } = new ObservableCollection<MvtTimeCodeModel>();
    public ObservableCollection<JudgeModel> JudgePositions { get; } = new();
    public ObservableCollection<JudgeModel> JudgePositionsGroupA { get; } = new();
    public ObservableCollection<JudgeModel> JudgePositionsGroupB { get; } = new();
    public ObservableCollection<JudgeModel> JudgePositionsGroupC { get; } = new();

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

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public double Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    public ObservableCollection<MvtCodeModel> RecognizedCodes { get; } = new();
    public ObservableCollection<MvtCodeModel> FilteredRecognizedCodes { get; } = new();
    public ObservableCollection<string> RecognizedGroups { get; } = new();
    public ObservableCollection<MvtCodeModel> MatchingCodes { get; } = new();

    public string SelectedRecognizedGroup
    {
        get => _selectedRecognizedGroup;
        set
        {
            if (SetProperty(ref _selectedRecognizedGroup, value))
            {
                ApplyRecognizedFilters();
            }
        }
    }

    public string SearchRecognizedText
    {
        get => _searchRecognizedText;
        set
        {
            if (SetProperty(ref _searchRecognizedText, value))
            {
                ApplyRecognizedFilters();
            }
        }
    }

    public TaoluInputMode CurrentTaoluInputMode
    {
        get => _currentTaoluInputMode;
        set
        {
            if (SetProperty(ref _currentTaoluInputMode, value))
            {
                OnPropertyChanged(nameof(IsCodeMode));
                OnPropertyChanged(nameof(IsPointMode));
                OnPropertyChanged(nameof(IsGroupCodesMode));
                OnPropertyChanged(nameof(IsCodesListMode));
                OnPropertyChanged(nameof(IsKeypadMode));
            }
        }
    }

    public bool IsCodeMode => CurrentTaoluInputMode == TaoluInputMode.Code;
    public bool IsPointMode => CurrentTaoluInputMode == TaoluInputMode.Points;
    public bool IsGroupCodesMode => CurrentTaoluInputMode == TaoluInputMode.GroupCodes;

    public bool IsCodesListMode
    {
        get => IsGroupCodesMode;
        set => CurrentTaoluInputMode = value ? TaoluInputMode.GroupCodes : TaoluInputMode.Code;
    }

    public bool IsKeypadMode => IsCodeMode;

    public string PointsInput
    {
        get => _pointsInput;
        set => SetProperty(ref _pointsInput, value);
    }

    public string GroupCodesButtonText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SelectedJudge?.Group))
            {
                return $"📋 Codes {SelectedJudge.Group}";
            }
            return Resources.GROUP_CODES_MODE;
        }
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
        IRepository<DrawPoolsModel> drawPoolsService,
        IRepository<MvtCodeModel> mvtCodesService
    ) : base(logger, resourceProvider, popupService)
    {
        _alertService = alertService;

        _bluetoothClient = bluetoothClient;
        _bluetoothService = bluetoothService;

        _competitionRepository = competitionRepository;
        _drawKnockoutService = drawKnockoutService;
        _drawOrderService = drawOrderService;
        _drawPoolsService = drawPoolsService;
        _mvtCodesService = mvtCodesService;

        _title = "JUDGE";

        for (int i = 1; i <= 5; i++)
        {
            JudgePositions.Add(new JudgeModel { Name = $"JUGE {i}", Number = i });
        }

        // Taolu positions: Groupe A (1..3), Groupe B (1..3), Groupe C (1..2)
        for (int i = 1; i <= 3; i++)
        {
            JudgePositionsGroupA.Add(new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "A" });
        }
        for (int i = 1; i <= 3; i++)
        {
            JudgePositionsGroupB.Add(new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "B" });
        }
        for (int i = 1; i <= 3; i++)
        {
            JudgePositionsGroupC.Add(new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "C" });
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
        Codes.CollectionChanged += (s, e) => UpdateCodesCanDelete();
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
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleDeviceConnectedAsync(deviceId);
        });
    }

    public async Task HandleDeviceConnectedAsync(string deviceId)
    {
        _serverDeviceId = deviceId;
        _lastServerDeviceId = deviceId;
        IsConnected = true;
        IsScanning = false;
        _lastServerContactTime = DateTime.UtcNow;
        _consecutiveFailedPings = 0;
        StopAutoReconnect();
        StartWatchdog();

        if (SelectedJudge == null)
        {
            IsReceivingCompetition = true;
            try
            {
                await _bluetoothClient.SendMessage(_serverDeviceId, Constants.Message.GET_COMPETITION);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting competition from server");
            }
        }
        else
        {
            // Reconnecting with an active judge
            try
            {
                string positionCode = !string.IsNullOrEmpty(SelectedJudge.Group)
                    ? $"{SelectedJudge.Group}_{SelectedJudge.Number}"
                    : SelectedJudge.Number.ToString();
                await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_POSITION}{positionCode}");

                await Task.Delay(150);

                if (HasUnsyncedChanges || _unsyncedScores.Count > 0 || Codes.Any(c => !c.IsSynced))
                {
                    await SyncUnsyncedScoresAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-registering judge position upon reconnection");
            }
        }
    }

    private void OnDeviceDisconnected(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleDeviceDisconnectedAsync(deviceId);
        });
    }

    public async Task HandleDeviceDisconnectedAsync(string? deviceId = null)
    {
        bool wasAlreadyOffline = !IsConnected && SelectedJudge != null;

        StopWatchdog();
        _consecutiveFailedPings = 0;

        if (!string.IsNullOrEmpty(deviceId))
        {
            _lastServerDeviceId = deviceId;
        }
        else if (!string.IsNullOrEmpty(_serverDeviceId))
        {
            _lastServerDeviceId = _serverDeviceId;
        }

        _serverDeviceId = null;
        IsConnected = false;
        IsReceivingCompetition = false;
        IsReceivingMatch = false;

        if (SelectedJudge == null)
        {
            CurrentCompetition = null;
            CompetitionType = CompetitionType.None;
            _lastMatchId = 0;
            Codes.Clear();
            _timer?.Stop();
            _pendingChunks.Clear();

            await _alertService.ShowToast("Le serveur s'est déconnecté.");
        }
        else if (!wasAlreadyOffline)
        {
            // Judge is actively in a match: keep local data, activate offline mode and background auto-reconnect
            StartAutoReconnect();
            await _alertService.ShowToast("Serveur déconnecté. Mode hors-ligne actif : continuez à noter.");
        }
    }

    public void StartWatchdog()
    {
        _lastServerContactTime = DateTime.UtcNow;
        _consecutiveFailedPings = 0;
        if (_watchdogTimer == null)
        {
            _watchdogTimer = Application.Current?.Dispatcher?.CreateTimer();
            if (_watchdogTimer != null)
            {
                _watchdogTimer.Interval = TimeSpan.FromSeconds(4);
                _watchdogTimer.Tick += async (s, e) =>
                {
                    await PerformWatchdogTickAsync();
                };
            }
        }
        _watchdogTimer?.Start();
    }

    public void StopWatchdog()
    {
        _watchdogTimer?.Stop();
        _consecutiveFailedPings = 0;
    }

    public async Task PerformWatchdogTickAsync()
    {
        if (!IsConnected || string.IsNullOrEmpty(_serverDeviceId))
        {
            return;
        }

        if (DateTime.UtcNow - _lastServerContactTime > TimeSpan.FromSeconds(12))
        {
            _logger.LogWarning("Server heartbeat timeout (>12s). Declaring server disconnected.");
            await HandleDeviceDisconnectedAsync(_serverDeviceId);
            return;
        }

        try
        {
            await _bluetoothClient.SendMessage(_serverDeviceId, Constants.Message.PING);
        }
        catch (Exception ex)
        {
            _consecutiveFailedPings++;
            _logger.LogWarning(ex, "Failed to send PING to server (attempt {Attempt}/3).", _consecutiveFailedPings);

            if (_consecutiveFailedPings >= 3)
            {
                _logger.LogWarning("3 consecutive PING attempts failed. Declaring server disconnected.");
                await HandleDeviceDisconnectedAsync(_serverDeviceId);
            }
        }
    }

    public void StartAutoReconnect()
    {
        if (_autoReconnectTimer == null)
        {
            _autoReconnectTimer = Application.Current?.Dispatcher?.CreateTimer();
            if (_autoReconnectTimer != null)
            {
                _autoReconnectTimer.Interval = TimeSpan.FromSeconds(3);
                _autoReconnectTimer.Tick += async (s, e) =>
                {
                    if (!IsConnected && SelectedJudge != null && !string.IsNullOrEmpty(_lastServerDeviceId))
                    {
                        await TryReconnectAsync();
                    }
                };
            }
        }
        _autoReconnectTimer?.Start();
    }

    public void StopAutoReconnect()
    {
        _autoReconnectTimer?.Stop();
    }

    [RelayCommand]
    public async Task Reconnect()
    {
        await TryReconnectAsync();
    }

    public async Task TryReconnectAsync()
    {
        if (IsConnected || _isReconnecting || string.IsNullOrEmpty(_lastServerDeviceId)) return;

        IsReconnecting = true;
        try
        {
            await _bluetoothClient.ConnectToDevice(_lastServerDeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed attempt to reconnect to server {DeviceId}", _lastServerDeviceId);
        }
        finally
        {
            IsReconnecting = false;
        }
    }

    public async Task SyncUnsyncedScoresAsync()
    {
        if (!IsConnected || _serverDeviceId == null || SelectedJudge == null) return;

        try
        {
            if (_unsyncedScores.Count > 0)
            {
                var scoresToSend = _unsyncedScores.ToList();
                foreach (var scoreItem in scoresToSend)
                {
                    var json = JsonSerializer.Serialize(scoreItem, _jsonOptions);
                    await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
                    _unsyncedScores.Remove(scoreItem);
                    if (scoreItem.Code != null)
                    {
                        scoreItem.Code.IsSynced = true;
                    }
                    await Task.Delay(50);
                }
            }
            else if (Codes.Any(c => !c.IsSynced))
            {
                int? competitorId = (CurrentDraw is DrawOrderModel o) ? o.CompetitorId : null;
                var unsynced = Codes.Where(c => !c.IsSynced).OrderBy(c => c.Date).ToList();
                foreach (var c in unsynced)
                {
                    var model = new TransfertScoreModel { Score = c.Score, CompetitorId = competitorId, Code = c };
                    var json = JsonSerializer.Serialize(model, _jsonOptions);
                    await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
                    c.IsSynced = true;
                    await Task.Delay(50);
                }
            }

            foreach (var c in Codes)
            {
                c.IsSynced = true;
            }

            await SendSyncStateAsync();
            HasUnsyncedChanges = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing unsynced scores with server");
            HasUnsyncedChanges = true;
        }
    }

    public async Task SendSyncStateAsync()
    {
        if (!IsConnected || _serverDeviceId == null || SelectedJudge == null) return;

        try
        {
            var syncState = new JudgeStateModel
            {
                Group = SelectedJudge.Group,
                Number = SelectedJudge.Number,
                Name = SelectedJudge.Name,
                Score = Score,
                RedPoints = SelectedJudge.RedPoints,
                BluePoints = SelectedJudge.BluePoints,
                Codes = Codes.ToList()
            };

            var json = JsonSerializer.Serialize(syncState, _jsonOptions);
            await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SYNC_STATE}{json}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending sync state to server");
        }
    }

    private void OnMessageReceived(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleMessageAsync(message);
        });
    }

    public async Task HandleMessageAsync(string message)
    {
        try
        {
            _lastServerContactTime = DateTime.UtcNow;
            _consecutiveFailedPings = 0;

            if (message == Constants.Message.PONG)
            {
                return;
            }

            if (message == Constants.Message.SERVER_STOPPED)
            {
                _logger.LogInformation("Server announced it has stopped.");
                await HandleDeviceDisconnectedAsync(_serverDeviceId);
                return;
            }

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
                    CurrentCompetition = competition;
                    CompetitionType = competition.Type;

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
            }
            else if (message.StartsWith(Constants.Message.MATCH_INFO))
            {
                var content = message.Substring(Constants.Message.MATCH_INFO.Length);
                var matchData = JsonSerializer.Deserialize<MatchInfoData>(content, _jsonOptions);

                if (matchData != null)
                {
                    bool isNewMatch = _lastMatchId != matchData.Id;
                    _lastMatchId = matchData.Id;

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
                    if (isNewMatch)
                    {
                        if (!HasUnsyncedChanges && _unsyncedScores.Count == 0 && !Codes.Any(c => !c.IsSynced))
                        {
                            Codes.Clear();
                            HasUnsyncedChanges = false;
                        }
                    }

                    if (CurrentDraw is DrawKnockoutModel k)
                    {
                        Competitor1 = k.Competitor1;
                        Competitor2 = k.Competitor2;
                    }
                    else if (CurrentDraw is DrawPoolsModel p)
                    {
                        Competitor1 = p.Competitor1;
                        Competitor2 = p.Competitor2;
                    }
                    else if (CurrentDraw is DrawOrderModel o)
                    {
                        Competitor1 = o.Competitor;
                        Competitor2 = null;
                    }
                    else
                    {
                        Competitor1 = null;
                        Competitor2 = null;
                    }

                    if (isNewMatch && SelectedJudge != null && CompetitionType == CompetitionType.Taolu && !HasUnsyncedChanges)
                    {
                        if (SelectedJudge.Group == "A") Score = 5.0;
                        else if (SelectedJudge.Group == "B") Score = 3.0;
                        else if (SelectedJudge.Group == "C") Score = 2.0;
                    }
                }
                IsReceivingMatch = false;
            }
            else if (message.StartsWith(Constants.Message.JUDGE_STATE))
            {
                if (HasUnsyncedChanges || _unsyncedScores.Count > 0 || Codes.Any(c => !c.IsSynced))
                {
                    // If we have offline changes, do not overwrite them with server state. Send/replay them immediately.
                    await SyncUnsyncedScoresAsync();
                    return;
                }

                var content = message.Substring(Constants.Message.JUDGE_STATE.Length);
                var state = JsonSerializer.Deserialize<JudgeStateModel>(content, _jsonOptions);
                if (state != null)
                {
                    Score = state.Score;
                    Codes.Clear();
                    if (state.Codes != null)
                    {
                        foreach (var c in state.Codes)
                        {
                            Codes.Add(c);
                        }
                    }
                    UpdateCodesCanDelete();

                    if (SelectedJudge != null)
                    {
                        SelectedJudge.Score = state.Score;
                        SelectedJudge.RedPoints = state.RedPoints;
                        SelectedJudge.BluePoints = state.BluePoints;
                    }
                }
            }
            else if (message == Constants.Message.JUDGE_SYNC_ACK)
            {
                HasUnsyncedChanges = false;
                await _alertService.ShowToast("Données synchronisées avec le serveur.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received message");
            await _alertService.ShowToast($"Error: {ex.Message}");
        }
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
                        //k.Draw = null;
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
                        //p.Draw = null;
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
            IsReceivingCompetition = true;
            await _bluetoothClient.ConnectToDevice(device.DeviceId);
        }
    }

    [RelayCommand]
    private async Task SelectPosition(JudgeModel judge)
    {
        SelectedJudge = judge;

        if (CompetitionType == CompetitionType.Taolu)
        {
            if (judge.Group == "A") Score = 5.0;
            else if (judge.Group == "B") Score = 3.0;
            else if (judge.Group == "C") Score = 2.0;
        }

        if (IsConnected && _serverDeviceId != null)
        {
            IsReceivingMatch = true;

            string positionCode = !string.IsNullOrEmpty(judge.Group) ? $"{judge.Group}_{judge.Number}" : judge.Number.ToString();
            try
            {
                await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_POSITION}{positionCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send JUDGE_POSITION to server. Transitioning to offline mode.");
                await HandleDeviceDisconnectedAsync(_serverDeviceId);
            }
        }
    }

    [RelayCommand]
    private async Task AddRedScore()
    {
        int? compId = Competitor1?.Id;
        if (compId == null && CurrentDraw is DrawKnockoutModel k) compId = k.Competitor1Id ?? k.Competitor1?.Id;
        else if (compId == null && CurrentDraw is DrawPoolsModel p) compId = p.Competitor1Id ?? p.Competitor1?.Id;
        await SendScore(compId, "Red");
    }

    [RelayCommand]
    private async Task AddBlueScore()
    {
        int? compId = Competitor2?.Id;
        if (compId == null && CurrentDraw is DrawKnockoutModel k) compId = k.Competitor2Id ?? k.Competitor2?.Id;
        else if (compId == null && CurrentDraw is DrawPoolsModel p) compId = p.Competitor2Id ?? p.Competitor2?.Id;
        await SendScore(compId, "Blue");
    }

    [RelayCommand]
    private async Task AddScore(object? param)
    {
        int? compId = null;
        string? side = null;
        if (param is int id)
        {
            compId = id;
            if (Competitor1?.Id == id) side = "Red";
            else if (Competitor2?.Id == id) side = "Blue";
        }
        else if (param is string s && (string.Equals(s, "Red", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Blue", StringComparison.OrdinalIgnoreCase)))
        {
            side = s;
            compId = string.Equals(s, "Red", StringComparison.OrdinalIgnoreCase) ? Competitor1?.Id : Competitor2?.Id;
        }
        else if (param is string sNum && int.TryParse(sNum, out int parsedId))
        {
            compId = parsedId;
            if (Competitor1?.Id == parsedId) side = "Red";
            else if (Competitor2?.Id == parsedId) side = "Blue";
        }
        await SendScore(compId, side);
    }

    private async Task SendScore(int? competitorId, string? side)
    {
        if (SelectedJudge != null)
        {
            if (string.Equals(side, "Red", StringComparison.OrdinalIgnoreCase)) SelectedJudge.RedPoints++;
            else if (string.Equals(side, "Blue", StringComparison.OrdinalIgnoreCase)) SelectedJudge.BluePoints++;
        }

        var scoreModel = new TransfertScoreModel() { Score = 1.0, CompetitorId = competitorId, Side = side };

        if (!IsConnected || _serverDeviceId == null)
        {
            _unsyncedScores.Add(scoreModel);
            HasUnsyncedChanges = true;
        }
        else
        {
            try
            {
                var json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
                await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send score to server; stored locally for future sync.");
                _unsyncedScores.Add(scoreModel);
                HasUnsyncedChanges = true;
            }
        }
    }

    [RelayCommand]
    private async Task TypeNumber(object? val)
    {
        if (val != null)
        {
            try
            {
                string number = val?.ToString() ?? string.Empty;

                if (!RecognizedCodes.Any())
                {
                    await LoadRecognizedCodesAsync();
                }

                // Append digit to current code
                Code += number;
                UpdateMatchingCodes(Code);

                // Auto-validate when there is only one possible code remaining
                var allMatches = RecognizedCodes
                    .Where(c => c.Code.StartsWith(Code, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (allMatches.Count == 1 && string.Equals(allMatches[0].Code, Code, StringComparison.OrdinalIgnoreCase))
                {
                    await ProcessCode(allMatches[0]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TypeNumber command");
            }
        }
    }

    [RelayCommand]
    private void BackspaceCode()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            Code = Code.Substring(0, Code.Length - 1);
            UpdateMatchingCodes(Code);
        }
    }

    [RelayCommand]
    private async Task ValidateCode()
    {
        if (!string.IsNullOrWhiteSpace(Code))
        {
            await ProcessTypedCode(Code);
        }
    }

    [RelayCommand]
    private void ClearCode()
    {
        Code = string.Empty;
        MatchingCodes.Clear();
    }

    [RelayCommand]
    private void SwitchMode(string mode)
    {
        if (string.Equals(mode, "Points", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "Point", StringComparison.OrdinalIgnoreCase))
        {
            CurrentTaoluInputMode = TaoluInputMode.Points;
        }
        else if (string.Equals(mode, "GroupCodes", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "Group", StringComparison.OrdinalIgnoreCase))
        {
            CurrentTaoluInputMode = TaoluInputMode.GroupCodes;
            ApplyJudgeGroupFilter();
        }
        else
        {
            CurrentTaoluInputMode = TaoluInputMode.Code;
        }
    }

    [RelayCommand]
    private void TypePoint(object? val)
    {
        if (val == null) return;
        string s = val.ToString() ?? string.Empty;
        if (s == "." || s == ",")
        {
            if (string.IsNullOrEmpty(PointsInput))
            {
                PointsInput = "0.";
            }
            else if (!PointsInput.Contains('.') && !PointsInput.Contains(','))
            {
                PointsInput += ".";
            }
        }
        else
        {
            if (PointsInput == "0" && s != ".")
            {
                PointsInput = s;
            }
            else
            {
                PointsInput += s;
            }
        }
    }

    [RelayCommand]
    private void ClearPoints()
    {
        PointsInput = string.Empty;
    }

    [RelayCommand]
    private void BackspacePoints()
    {
        if (!string.IsNullOrEmpty(PointsInput))
        {
            PointsInput = PointsInput.Substring(0, PointsInput.Length - 1);
        }
    }

    [RelayCommand]
    private async Task DeductPoints(object? val)
    {
        double pts = 0;
        if (val is double d)
        {
            pts = d;
        }
        else if (val is string s && double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
        {
            pts = parsedVal;
        }
        else if (!string.IsNullOrWhiteSpace(PointsInput) && double.TryParse(PointsInput.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedInput))
        {
            pts = parsedInput;
        }

        if (pts <= 0) return;

        pts = Math.Round(pts, 2);
        Score = Math.Round(Math.Max(0, Score - pts), 2);

        int? competitorId = null;
        if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
        {
            competitorId = ((DrawOrderModel)CurrentDraw).CompetitorId;
        }

        var syntheticCode = new MvtCodeModel
        {
            Code = $"-{pts.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)}",
            Movement = "Déduction libre",
            Description = $"Déduction de {pts.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)} pt(s)",
            Value = pts,
            Category = "Points"
        };

        var data = new MvtTimeCodeModel { Date = DateTime.Now, Code = syntheticCode, Score = Score, IsSynced = false };
        Codes.Insert(0, data);

        PointsInput = string.Empty;

        var scoreModel = new TransfertScoreModel { Score = Score, CompetitorId = competitorId, Code = data };

        if (!IsConnected || _serverDeviceId == null)
        {
            _unsyncedScores.Add(scoreModel);
            HasUnsyncedChanges = true;
        }
        else
        {
            try
            {
                var json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
                await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
                data.IsSynced = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send points deduction to server immediately; stored locally for future sync.");
                _unsyncedScores.Add(scoreModel);
                HasUnsyncedChanges = true;
            }
        }
    }

    [RelayCommand]
    private async Task OpenTaoluSettings()
    {
        await Shell.Current.GoToAsync("TaoluCodesPage");
    }

    [RelayCommand]
    private async Task ApplyCode(MvtCodeModel? code)
    {
        if (code == null) return;
        await ProcessCode(code);
    }

    [RelayCommand]
    private async Task DeleteCode(MvtTimeCodeModel code)
    {
        if (code == null) return;
        Codes.Remove(code);

        if (code.Code != null)
        {
            int? competitorId = null;
            if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
            {
                competitorId = ((DrawOrderModel)CurrentDraw).CompetitorId;
            }

            Score = Math.Round(Score + Math.Abs(code.Code.Value), 2);
            var scoreModel = new TransfertScoreModel() { Score = Score, CompetitorId = competitorId };

            if (!IsConnected || _serverDeviceId == null)
            {
                _unsyncedScores.Add(scoreModel);
                HasUnsyncedChanges = true;
            }
            else
            {
                try
                {
                    var json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
                    await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send deleted code score to server immediately; stored locally for future sync.");
                    _unsyncedScores.Add(scoreModel);
                    HasUnsyncedChanges = true;
                }
            }
        }
    }

    private void UpdateCodesCanDelete()
    {
        for (int i = 0; i < Codes.Count; i++)
        {
            Codes[i].CanDelete = (i == 0);
        }
    }

    private async Task ProcessTypedCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return;

        var findCode = RecognizedCodes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? (await _mvtCodesService.GetAllAsync())?.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

        if (findCode == null)
        {
            await _alertService.ShowToast("Code inconnu");
        }
        else
        {
            await ProcessCode(findCode);
        }
    }

    private async Task ProcessCode(MvtCodeModel findCode)
    {
        Score = Math.Round(Math.Max(0, Score - Math.Abs(findCode.Value)), 2);
        int? competitorId = null;

        if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
        {
            competitorId = ((DrawOrderModel)CurrentDraw).CompetitorId;
        }

        var data = new MvtTimeCodeModel() { Date = DateTime.Now, Code = findCode, Score = Score, IsSynced = false };
        Codes.Insert(0, data);

        Code = string.Empty;
        MatchingCodes.Clear();

        var scoreModel = new TransfertScoreModel() { Score = Score, CompetitorId = competitorId, Code = data };

        if (!IsConnected || _serverDeviceId == null)
        {
            _unsyncedScores.Add(scoreModel);
            HasUnsyncedChanges = true;
        }
        else
        {
            try
            {
                var json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
                await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
                data.IsSynced = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send code to server immediately; stored locally for future sync.");
                _unsyncedScores.Add(scoreModel);
                HasUnsyncedChanges = true;
            }
        }
    }

    public async Task LoadRecognizedCodesAsync()
    {
        try
        {
            var list = await _mvtCodesService.GetAllAsync();
            if (list == null || !list.Any() || list.All(c => string.IsNullOrEmpty(c.Movement) && string.IsNullOrEmpty(c.GroupName)))
            {
                foreach (var defaultCode in MvtCodeModel.DefaultCodes)
                {
                    await _mvtCodesService.AddAsync(new MvtCodeModel
                    {
                        Code = defaultCode.Code,
                        GroupName = defaultCode.GroupName,
                        Movement = defaultCode.Movement,
                        Description = defaultCode.Description,
                        Value = defaultCode.Value,
                        Category = defaultCode.Category
                    });
                }
                list = await _mvtCodesService.GetAllAsync();
            }

            RecognizedCodes.Clear();
            if (list != null)
            {
                foreach (var c in list.OrderBy(c => c.Code))
                {
                    RecognizedCodes.Add(c);
                }
            }

            UpdateRecognizedGroups();
            ApplyRecognizedFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recognized codes in JudgeViewModel");
        }
    }

    private void UpdateRecognizedGroups()
    {
        var current = SelectedRecognizedGroup;
        RecognizedGroups.Clear();
        RecognizedGroups.Add(Resources.ALL_GROUPS);

        if (!string.IsNullOrWhiteSpace(SelectedJudge?.Group))
        {
            string judgeGroupTitle = SelectedJudge.Group.ToUpperInvariant() switch
            {
                "A" => "Groupe A (Qualité des mouvements)",
                "B" => "Groupe B (Performance globale)",
                "C" => "Groupe C (Degré de difficulté)",
                _ => $"Groupe {SelectedJudge.Group}"
            };
            RecognizedGroups.Add(judgeGroupTitle);
        }

        var groups = RecognizedCodes
            .Select(c => c.GroupLabel)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g);

        foreach (var g in groups)
        {
            RecognizedGroups.Add(g);
        }

        if (!string.IsNullOrEmpty(current) && RecognizedGroups.Contains(current))
        {
            SelectedRecognizedGroup = current;
        }
        else
        {
            SelectedRecognizedGroup = RecognizedGroups.FirstOrDefault() ?? Resources.ALL_GROUPS;
        }
    }

    private void ApplyJudgeGroupFilter()
    {
        if (!string.IsNullOrWhiteSpace(SelectedJudge?.Group))
        {
            string judgeGroupPrefix = $"Groupe {SelectedJudge.Group.ToUpperInvariant()}";
            var matching = RecognizedGroups.FirstOrDefault(g => g.StartsWith(judgeGroupPrefix, StringComparison.OrdinalIgnoreCase));
            if (matching != null)
            {
                SelectedRecognizedGroup = matching;
                return;
            }
        }
        SelectedRecognizedGroup = Resources.ALL_GROUPS;
    }

    public void ApplyRecognizedFilters()
    {
        FilteredRecognizedCodes.Clear();
        var query = RecognizedCodes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedRecognizedGroup) && SelectedRecognizedGroup != Resources.ALL_GROUPS)
        {
            if (SelectedRecognizedGroup.StartsWith("Groupe A", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => CodeMatchesJudgeGroup(c, "A"));
            }
            else if (SelectedRecognizedGroup.StartsWith("Groupe B", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => CodeMatchesJudgeGroup(c, "B"));
            }
            else if (SelectedRecognizedGroup.StartsWith("Groupe C", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => CodeMatchesJudgeGroup(c, "C"));
            }
            else
            {
                query = query.Where(c => string.Equals(c.GroupLabel, SelectedRecognizedGroup, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchRecognizedText))
        {
            var search = SearchRecognizedText.Trim().ToLowerInvariant();
            query = query.Where(c =>
                (c.Code != null && c.Code.ToLowerInvariant().Contains(search)) ||
                (c.Movement != null && c.Movement.ToLowerInvariant().Contains(search)) ||
                (c.Description != null && c.Description.ToLowerInvariant().Contains(search)) ||
                (c.GroupLabel != null && c.GroupLabel.ToLowerInvariant().Contains(search)));
        }

        foreach (var c in query.OrderBy(c => c.Code))
        {
            FilteredRecognizedCodes.Add(c);
        }
    }

    private static bool CodeMatchesJudgeGroup(MvtCodeModel code, string? judgeGroup)
    {
        if (string.IsNullOrWhiteSpace(judgeGroup)) return true;

        judgeGroup = judgeGroup.Trim().ToUpperInvariant();
        if (judgeGroup == "A")
        {
            return string.Equals(code.GroupLabel, "Techniques de main", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupLabel, "Equilibres", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupLabel, "Techniques de jambe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupLabel, "Positions", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupLabel, "Techniques spécifiques aux armes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupName, "A", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupName, "Groupe A", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.Category, "Groupe A", StringComparison.OrdinalIgnoreCase);
        }
        else if (judgeGroup == "B")
        {
            return string.Equals(code.GroupLabel, "Autres erreurs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupLabel, "Performance globale", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupName, "B", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupName, "Groupe B", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.Category, "Groupe B", StringComparison.OrdinalIgnoreCase);
        }
        else if (judgeGroup == "C")
        {
            return string.Equals(code.GroupLabel, "Techniques de saut", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupLabel, "Degré de difficulté", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupLabel, "Difficultés", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupName, "C", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.GroupName, "Groupe C", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code.Category, "Groupe C", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private void UpdateMatchingCodes(string prefix)
    {
        MatchingCodes.Clear();
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            var matches = RecognizedCodes
                .Where(c => c.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Take(6);

            foreach (var m in matches)
            {
                MatchingCodes.Add(m);
            }
        }
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        _typeNumberCts = new CancellationTokenSource();

        _bluetoothClient.DeviceDiscovered += OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected += OnDeviceConnected;
        _bluetoothClient.DeviceDisconnected += OnDeviceDisconnected;
        _bluetoothClient.MessageReceived += OnMessageReceived;

        await LoadRecognizedCodesAsync();
        await base.OnAppearing();
    }

    public override async Task OnDisappearing()
    {
        _bluetoothClient.DeviceDiscovered -= OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected -= OnDeviceConnected;
        _bluetoothClient.DeviceDisconnected -= OnDeviceDisconnected;
        _bluetoothClient.MessageReceived -= OnMessageReceived;

        if (IsConnected && _serverDeviceId != null)
        {
            try
            {
                await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_DISCONNECT}");
                await Task.Delay(150);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending JUDGE_DISCONNECT");
            }

            try
            {
                await _bluetoothClient.DisconnectFromDevice(_serverDeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from device {DeviceId}", _serverDeviceId);
            }
        }

        if (IsScanning)
        {
            try
            {
                await _bluetoothClient.StopScan();
            }
            catch { }
            IsScanning = false;
        }

        _serverDeviceId = null;
        IsConnected = false;
        IsReceivingCompetition = false;
        IsReceivingMatch = false;
        CurrentCompetition = null;
        CompetitionType = CompetitionType.None;
        SelectedJudge = null;
        _timer?.Stop();
        _pendingChunks.Clear();
        _unsyncedScores.Clear();

        _typeNumberCts?.Cancel();
        StopWatchdog();
        StopAutoReconnect();

        await base.OnDisappearing();
    }
    #endregion
}
