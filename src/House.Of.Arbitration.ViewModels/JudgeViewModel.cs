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

    private double _score = 7.0;

    private string _code = "";
    private CancellationTokenSource? _typeNumberCts;
    private IDispatcherTimer? _typeTimer;

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
    private bool _isCodesListMode = false;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public CompetitionModel? CurrentCompetition
    {
        get => _currentCompetition;
        set
        {
            if (SetProperty(ref _currentCompetition, value))
            {
                OnPropertyChanged(nameof(CanSelectPosition));
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
            }
        }
    }

    public bool IsReceivingCompetition
    {
        get => _isReceivingCompetition;
        set
        {
            if (SetProperty(ref _isReceivingCompetition, value))
            {
                OnPropertyChanged(nameof(CanSelectPosition));
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

    public bool IsCodesListMode
    {
        get => _isCodesListMode;
        set
        {
            if (SetProperty(ref _isCodesListMode, value))
            {
                OnPropertyChanged(nameof(IsKeypadMode));
            }
        }
    }

    public bool IsKeypadMode => !IsCodesListMode;
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
            _serverDeviceId = deviceId;
            IsConnected = true;
            IsScanning = false;
            IsReceivingCompetition = true;

            try
            {
                await _bluetoothClient.SendMessage(_serverDeviceId, Constants.Message.GET_COMPETITION);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting competition from server");
            }
        });
    }

    private void OnDeviceDisconnected(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _serverDeviceId = null;
            IsConnected = false;
            IsReceivingCompetition = false;
            IsReceivingMatch = false;
            CurrentCompetition = null;
            CompetitionType = CompetitionType.None;
            SelectedJudge = null;
            _timer?.Stop();
            _pendingChunks.Clear();

            await _alertService.ShowAlert("Déconnexion", "Le serveur s'est arrêté.", "OK");
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
                        Codes.Clear();

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

                        if (SelectedJudge != null && CompetitionType == CompetitionType.Taolu)
                        {
                            if (SelectedJudge.Group == "A") Score = 5.0;
                            else if (SelectedJudge.Group == "B") Score = 3.0;
                            else if (SelectedJudge.Group == "C") Score = 2.0;
                        }
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
            await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_POSITION}{positionCode}");
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
        else if (param is string s && int.TryParse(s, out int parsedId))
        {
            compId = parsedId;
            if (Competitor1?.Id == parsedId) side = "Red";
            else if (Competitor2?.Id == parsedId) side = "Blue";
        }
        await SendScore(compId, side);
    }

    private async Task SendScore(int? competitorId, string? side)
    {
        var json = JsonSerializer.Serialize(new TransfertScoreModel() { Score = 1.0, CompetitorId = competitorId, Side = side }, _jsonOptions);

        if (IsConnected && _serverDeviceId != null)
        {
            await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
        }
    }

    [RelayCommand]
    private async Task TypeNumber(object? val)
    {
        if (val != null)
        {
            try
            {
                string number = val?.ToString() ?? String.Empty;

                // Append digit to current code
                Code += number;
                UpdateMatchingCodes(Code);

                // Initialize timer if needed
                if (_typeTimer == null)
                {
                    _typeTimer = Application.Current?.Dispatcher.CreateTimer();
                    if (_typeTimer != null)
                    {
                        _typeTimer.Interval = TimeSpan.FromMilliseconds(500);
                        _typeTimer.Tick += async (s, e) =>
                        {
                            _typeTimer.Stop();
                            if (!string.IsNullOrEmpty(Code))
                            {
                                var fullCode = Code;
                                await ProcessTypedCode(fullCode);
                            }
                        };
                    }
                }
                // Restart timer on each new digit
                _typeTimer?.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TypeNumber command");
            }
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
        IsCodesListMode = string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase);
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
            Score = Math.Round(Score + Math.Abs(code.Code.Value), 1);

            if (IsConnected && _serverDeviceId != null)
            {
                int? competitorId = null;
                if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
                {
                    competitorId = ((DrawOrderModel)CurrentDraw).CompetitorId;
                }

                var json = JsonSerializer.Serialize(new TransfertScoreModel() { Score = Score, CompetitorId = competitorId }, _jsonOptions);
                await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
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
            Code = string.Empty;
            MatchingCodes.Clear();
        }
        else
        {
            await ProcessCode(findCode);
        }
    }

    private async Task ProcessCode(MvtCodeModel findCode)
    {
        Score = Math.Round(Score - Math.Abs(findCode.Value), 1);
        int? competitorId = null;

        if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
        {
            competitorId = ((DrawOrderModel)CurrentDraw).CompetitorId;
        }

        var data = new MvtTimeCodeModel() { Date = DateTime.Now, Code = findCode, Score = Score };
        Codes.Insert(0, data);

        Code = string.Empty;
        MatchingCodes.Clear();

        if (IsConnected && _serverDeviceId != null)
        {
            var json = JsonSerializer.Serialize(new TransfertScoreModel() { Score = Score, CompetitorId = competitorId, Code = data }, _jsonOptions);
            await _bluetoothClient.SendMessage(_serverDeviceId, $"{Constants.Message.JUDGE_SCORE}{json}");
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

    public void ApplyRecognizedFilters()
    {
        FilteredRecognizedCodes.Clear();
        var query = RecognizedCodes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedRecognizedGroup) && SelectedRecognizedGroup != Resources.ALL_GROUPS)
        {
            query = query.Where(c => string.Equals(c.GroupLabel, SelectedRecognizedGroup, StringComparison.OrdinalIgnoreCase));
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

        _typeNumberCts?.Cancel();
        _typeNumberCts?.Dispose();
        _typeNumberCts = null;

        await base.OnDisappearing();
    }
    #endregion
}
