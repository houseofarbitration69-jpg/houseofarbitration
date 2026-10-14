#region Imports

using CommunityToolkit.Maui;

using CommunityToolkit.Mvvm.Input;

using House.Of.Arbitration.Data.Abstractions;

using House.Of.Arbitration.Localization;

using House.Of.Arbitration.Models;

using House.Of.Arbitration.Services.Abstractions;

using House.Of.Arbitration.ViewModels.Core;

using Microsoft.Extensions.Logging;

using System.Text.Json;

using System.Text.Json.Serialization;

#endregion



namespace House.Of.Arbitration.ViewModels;



[QueryProperty(nameof(CompetitionId), "CompetitionId")]

public partial class ServerViewModel : BaseViewModel

{

    #region Services

    private readonly IAlertService _alertService;

    private readonly IBluetoothService _bluetoothService;

    private readonly IBluetoothServer _bluetoothServer;

    private readonly IRepository<CompetitionModel> _competitionRepository;

    private readonly IRepository<DrawKnockoutModel> _drawKnockoutService;

    private readonly IRepository<DrawOrderModel> _drawOrderService;

    private readonly IRepository<DrawPoolsModel> _drawPoolsModel;

    private readonly IRepository<RefereeDataModel> _refereeDataRepository;

    #endregion



    #region Attributs

    private string _title = String.Empty;

    private bool _bluetoothAvailable = false;

    private string _serverName = String.Empty;



    private System.Collections.ObjectModel.ObservableCollection<object>? _draws;

    private IDrawModel? _currentDraw;

    private System.Collections.ObjectModel.ObservableCollection<JudgeModel> _judges = new();

    private System.Collections.ObjectModel.ObservableCollection<RoundResultModel> _roundResults = new();



    private TimeSpan _timeLeft = TimeSpan.FromMinutes(2);

    private bool _isTimerRunning;

    private bool _isServerStarted;

    private IDispatcherTimer? _timer;



    private int _competitionId;

    private int _totalMatches;

    private int _currentMatchNumber;



    private CompetitionModel? _currentCompetition;



    private readonly Dictionary<string, JudgeModel> _clientJudgeMapping = new();



    private readonly JsonSerializerOptions _jsonOptions = new()

    {

        ReferenceHandler = ReferenceHandler.IgnoreCycles,

        WriteIndented = false

    };

    #endregion



    #region Properties

    public string Title

    {

        get => _title;

        set => SetProperty(ref _title, value);

    }



    public bool BluetoothAvailable

    {

        get => _bluetoothAvailable;

        set => SetProperty(ref _bluetoothAvailable, value);

    }



    public string ServerName

    {

        get => _serverName;

        set => SetProperty(ref _serverName, value);

    }



    public System.Collections.ObjectModel.ObservableCollection<object>? Draws

    {

        get => _draws;

        set => SetProperty(ref _draws, value);

    }



    public System.Collections.ObjectModel.ObservableCollection<JudgeModel> Judges

    {

        get => _judges;

        set => SetProperty(ref _judges, value);

    }



    private System.Collections.ObjectModel.ObservableCollection<JudgeGroupModel> _judgeGroups = new();

    public System.Collections.ObjectModel.ObservableCollection<JudgeGroupModel> JudgeGroups

    {

        get => _judgeGroups;

        set => SetProperty(ref _judgeGroups, value);

    }



    public System.Collections.ObjectModel.ObservableCollection<RoundResultModel> RoundResults

    {

        get => _roundResults;

        set => SetProperty(ref _roundResults, value);

    }



    public string TimeLeftDisplay => _timeLeft.ToString(@"mm\:ss");



    public bool IsTimerRunning

    {

        get => _isTimerRunning;

        set => SetProperty(ref _isTimerRunning, value);

    }



    public bool IsServerStarted

    {

        get => _isServerStarted;

        set => SetProperty(ref _isServerStarted, value);

    }



    public IDrawModel? CurrentDraw

    {

        get => _currentDraw;

        set => SetProperty(ref _currentDraw, value);

    }



    public int CompetitionId

    {

        get => _competitionId;

        set => SetProperty(ref _competitionId, value);

    }



    public int TotalMatches

    {

        get => _totalMatches;

        set => SetProperty(ref _totalMatches, value);

    }



    public int CurrentMatchNumber

    {

        get => _currentMatchNumber;

        set => SetProperty(ref _currentMatchNumber, value);

    }



    private double _currentRedVotes;

    public double CurrentRedVotes

    {

        get => _currentRedVotes;

        set => SetProperty(ref _currentRedVotes, value);

    }



    private double _currentBlueVotes;

    public double CurrentBlueVotes

    {

        get => _currentBlueVotes;

        set => SetProperty(ref _currentBlueVotes, value);

    }



    #endregion



    #region Constructors

    public ServerViewModel(

        ILogger<ServerViewModel> logger,

        ResourceProvider resourceProvider,

        IPopupService popupService,

        IAlertService alertService,

        IBluetoothService bluetoothService,

        IBluetoothServer bluetoothServer,

        IRepository<CompetitionModel> competitionRepository,

        IRepository<DrawKnockoutModel> drawKnockoutService,

        IRepository<DrawOrderModel> drawOrderService,

        IRepository<DrawPoolsModel> drawPoolsService,

        IRepository<RefereeDataModel> refereeDataRepository

    ) : base(logger, resourceProvider, popupService)

    {

        Title = "SERVER";



        _alertService = alertService;



        _bluetoothService = bluetoothService;

        _bluetoothServer = bluetoothServer;



        var serverId = _bluetoothServer.InstanceId.ToString().Substring(0, 8);



        _competitionRepository = competitionRepository;

        _drawKnockoutService = drawKnockoutService;

        _drawOrderService = drawOrderService;

        _drawPoolsModel = drawPoolsService;

        _refereeDataRepository = refereeDataRepository;



        for (int i = 1; i <= 5; i++)

        {

            Judges.Add(new JudgeModel { Name = $"JUGE {i}", Number = i });

        }



        _timer = Application.Current?.Dispatcher.CreateTimer();

        if (_timer != null)

        {

            _timer.Interval = TimeSpan.FromSeconds(1);

            _timer.Tick += async (s, e) =>

            {

                if (_timeLeft.TotalSeconds > 0)

                {

                    _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));

                    OnPropertyChanged(nameof(TimeLeftDisplay));

                }

                else

                {

                    StopTimer();

                    await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_STOP);

                }

            };

        }

    }

    #endregion



    #region Event Handlers

    private async void OnDeviceConnected(object? sender, string clientId)

    {

        await SendCompetitionDataToClientAsync(clientId);

    }



    private void OnDeviceDisconnected(object? sender, string clientId)

    {

        MainThread.BeginInvokeOnMainThread(() =>

        {

            if (_clientJudgeMapping.TryGetValue(clientId, out var judge))
            {
                judge.IsConnected = false;
                _clientJudgeMapping.Remove(clientId);
                OnPropertyChanged(nameof(Judges));
            }
        });

    }



    private async void OnMessageReceived(object? sender, (string ClientId, string Message) args)

    {

        MainThread.BeginInvokeOnMainThread(async () =>

        {

            if (args.Message == Constants.Message.GET_COMPETITION)

            {

                await SendCompetitionDataToClientAsync(args.ClientId);

                return;

            }



            if (args.Message.StartsWith(Constants.Message.JUDGE_POSITION))

            {

                var posStr = args.Message.Substring(Constants.Message.JUDGE_POSITION.Length);

                JudgeModel? judge = null;



                var parts = posStr.Split('_');

                if (parts.Length == 2 && int.TryParse(parts[1], out int num))

                {

                    judge = Judges.FirstOrDefault(j => j.Group == parts[0] && j.Number == num);

                }

                else if (int.TryParse(posStr, out int position))

                {

                    judge = Judges.FirstOrDefault(j => string.IsNullOrEmpty(j.Group) && j.Number == position)

                         ?? Judges.FirstOrDefault(j => j.Number == position);

                }

                else

                {

                    judge = Judges.FirstOrDefault(j => j.Name == posStr);

                }



                if (judge != null)

                {

                    // Remove previous mapping for this client if any

                    if (_clientJudgeMapping.TryGetValue(args.ClientId, out var oldJudge))

                    {

                        oldJudge.IsConnected = false;

                    }



                    // Remove mapping for this judge if another client was using it

                    var existingMapping = _clientJudgeMapping.FirstOrDefault(x => x.Value == judge);

                    if (existingMapping.Key != null)

                    {

                        _clientJudgeMapping.Remove(existingMapping.Key);

                    }



                    judge.IsConnected = true;
                    _clientJudgeMapping[args.ClientId] = judge;
                    OnPropertyChanged(nameof(Judges));

                    // Envoi du match courant
                    if (CurrentDraw != null)
                    {
                        try
                        {
                            var json = JsonSerializer.Serialize(new MatchInfoData { Id = CurrentDraw.Id, Type = CurrentDraw.Type }, _jsonOptions);
                            await _bluetoothServer.SendToClientAsync($"{Constants.Message.MATCH_INFO}{json}", args.ClientId);
                            await _bluetoothServer.SendToClientAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}", args.ClientId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending match info to client {ClientId}", args.ClientId);
                        }
                    }
                    else
                    {
                        await _bluetoothServer.SendToClientAsync($"{Constants.Message.MATCH_INFO}{{}}", args.ClientId);
                    }
                }
            }
            else if (args.Message.StartsWith(Constants.Message.JUDGE_DISCONNECT))
            {
                if (_clientJudgeMapping.TryGetValue(args.ClientId, out var judge))
                {
                    judge.IsConnected = false;
                    _clientJudgeMapping.Remove(args.ClientId);
                    OnPropertyChanged(nameof(Judges));
                }
            }
            else if (args.Message.StartsWith(Constants.Message.JUDGE_SCORE))
            {
                if (_clientJudgeMapping.TryGetValue(args.ClientId, out var judge))
                {
                    try
                    {
                        var json = args.Message.Substring(Constants.Message.JUDGE_SCORE.Length);
                        var scoreData = JsonSerializer.Deserialize<TransfertScoreModel>(json, _jsonOptions);

                        if (scoreData != null && CurrentDraw != null && CurrentDraw.Type != RoundType.Order)
                        {
                            var competitor1 = GetCompetitor(CurrentDraw, true);
                            var competitor2 = GetCompetitor(CurrentDraw, false);

                            bool isRed = (competitor1 != null && scoreData.CompetitorId == competitor1.Id) || string.Equals(scoreData.Side, "Red", StringComparison.OrdinalIgnoreCase);
                            bool isBlue = (competitor2 != null && scoreData.CompetitorId == competitor2.Id) || string.Equals(scoreData.Side, "Blue", StringComparison.OrdinalIgnoreCase);

                            if (isRed)
                            {
                                judge.RedPoints += scoreData.Score;
                                if (competitor1 != null)
                                {
                                    await SaveRefereeData(judge, competitor1.Id, scoreData.Score);
                                }
                                OnPropertyChanged(nameof(Judges));
                                UpdateCurrentRoundVotes();
                            }
                            else if (isBlue)
                            {
                                judge.BluePoints += scoreData.Score;
                                if (competitor2 != null)
                                {
                                    await SaveRefereeData(judge, competitor2.Id, scoreData.Score);
                                }
                                OnPropertyChanged(nameof(Judges));
                                UpdateCurrentRoundVotes();
                            }
                        }
                        else if (scoreData != null && CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
                        {
                            judge.Score = scoreData.Score;
                            OnPropertyChanged(nameof(Judges));
                            var competitor = GetCompetitor(CurrentDraw, true);

                            if (competitor != null)
                            {
                                await SaveRefereeData(judge, competitor.Id, scoreData.Score, false, scoreData.Code);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing judge score from client {ClientId}", args.ClientId);
                    }
                }
            }

        });

    }



    [RelayCommand]

    private async Task BroadcastMatchInfo()

    {

        if (CurrentDraw != null)

        {

            try

            {

                var json = JsonSerializer.Serialize(new { Id = CurrentDraw.Id, Type = CurrentDraw.Type }, _jsonOptions);

                await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_INFO}{json}");

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error broadcasting match info");

            }

        }

    }



    private CompetitorModel? GetCompetitor(IDrawModel? draw, bool red)
    {
        if (draw is DrawKnockoutModel k) return red ? k.Competitor1 : k.Competitor2;
        if (draw is DrawPoolsModel p) return red ? p.Competitor1 : p.Competitor2;
        if (draw is DrawOrderModel o) return o.Competitor;
        return null;
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        _bluetoothServer.DeviceConnected -= OnDeviceConnected;
        _bluetoothServer.DeviceDisconnected -= OnDeviceDisconnected;
        _bluetoothServer.MessageReceived -= OnMessageReceived;

        _bluetoothServer.DeviceConnected += OnDeviceConnected;
        _bluetoothServer.DeviceDisconnected += OnDeviceDisconnected;
        _bluetoothServer.MessageReceived += OnMessageReceived;

        CheckBluetoothAvailabilityCommand.Execute(null);

        if (!IsServerStarted)
        {
            await Init();
        }
    }

    public override Task OnDisappearing()
    {
        return base.OnDisappearing();
    }
    #endregion

    #region Private Methods
    private void ResetJudgeScores()
    {
        foreach (var judge in Judges)
        {
            judge.RedPoints = 0;
            judge.BluePoints = 0;
            if (judge.Group == "A") judge.Score = 5.0;
            else if (judge.Group == "B") judge.Score = 3.0;
            else if (judge.Group == "C") judge.Score = 2.0;
            else judge.Score = 0.0;
        }
        OnPropertyChanged(nameof(Judges));
        UpdateCurrentRoundVotes();
    }

    private void UpdateCurrentRoundVotes()
    {
        CurrentRedVotes = Judges.Sum(j => j.RedPoints);
        CurrentBlueVotes = Judges.Sum(j => j.BluePoints);
    }

    private void SetupJudges(CompetitionType competitionType)
    {
        JudgeGroups.Clear();

        if (competitionType == CompetitionType.Taolu)
        {
            Judges.Clear();
            var groupA = new JudgeGroupModel("Groupe A", "Qualité", 5.0);
            var groupB = new JudgeGroupModel("Groupe B", "Performance", 3.0);
            var groupC = new JudgeGroupModel("Groupe C", "Difficulté", 2.0);

            // Groupe A (1..3) - 5 points
            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "A", Score = 5.0 };
                Judges.Add(judge);
                groupA.Judges.Add(judge);
            }

            // Groupe B (1..3) - 3 points
            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "B", Score = 3.0 };
                Judges.Add(judge);
                groupB.Judges.Add(judge);
            }

            // Groupe C (1..3) - 2 points
            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE {i}", Number = i, Group = "C", Score = 2.0 };
                Judges.Add(judge);
                groupC.Judges.Add(judge);
            }

            JudgeGroups.Add(groupA);
            JudgeGroups.Add(groupB);
            JudgeGroups.Add(groupC);
        }
        else
        {
            if (Judges.Count == 5 && Judges.All(j => string.IsNullOrEmpty(j.Group)))
            {
                for (int i = 0; i < 5; i++)
                {
                    Judges[i].Name = $"JUGE {i + 1}";
                    Judges[i].Number = i + 1;
                    Judges[i].Group = null;
                }
            }
            else
            {
                Judges.Clear();
                for (int i = 1; i <= 5; i++)
                {
                    Judges.Add(new JudgeModel { Name = $"JUGE {i}", Number = i });
                }
            }
        }
        OnPropertyChanged(nameof(Judges));
    }



    private async Task SendCompetitionDataToClientAsync(string clientId)

    {

        if (_currentCompetition == null && CompetitionId > 0)

        {

            _currentCompetition = await LoadCompetitionDataAsync(CompetitionId);

        }

        if (_currentCompetition == null && CurrentDraw?.Draw?.Category?.CompetitionId > 0)

        {

            _currentCompetition = await LoadCompetitionDataAsync(CurrentDraw.Draw.Category.CompetitionId.Value);

        }

        if (_currentCompetition == null)

        {

            var firstComp = (await _competitionRepository.GetAllAsync())?.FirstOrDefault();

            if (firstComp != null)

            {

                _currentCompetition = await LoadCompetitionDataAsync(firstComp.Id);

            }

        }



        if (_currentCompetition != null)

        {

            try

            {

                var json = JsonSerializer.Serialize(_currentCompetition, _jsonOptions);

                await _bluetoothServer.SendToClientAsync($"{Constants.Message.COMPETITION_DATA}{json}", clientId);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error sending competition data to client {ClientId}", clientId);

            }

        }

    }



    private Task<CompetitionModel?> LoadCompetitionDataAsync(int competitionId)

    {

        return _competitionRepository.GetByIdAsync(competitionId,

            "Categories.AgeRange",

            "Categories.Competitors.Competitor.Country",

            "Categories.Draw.DrawKnockouts.Competitor1.Country",

            "Categories.Draw.DrawKnockouts.Competitor2.Country",

            "Categories.Draw.DrawKnockouts.Winner",

            "Categories.Draw.DrawKnockouts.Looser",

            "Categories.Draw.DrawOrders.Competitor.Country",

            "Categories.Draw.DrawPools.Competitor1.Country",

            "Categories.Draw.DrawPools.Competitor2.Country",

            "Categories.Draw.DrawPools.Winner",

            "Categories.Draw.DrawPools.Looser");

    }



    private async Task Init()

    {

        if (CompetitionId > 0)

        {

            _currentCompetition = await LoadCompetitionDataAsync(CompetitionId);

        }

        if (_currentCompetition == null)

        {

            var firstComp = (await _competitionRepository.GetAllAsync())?.FirstOrDefault();

            if (firstComp != null)

            {

                _currentCompetition = await LoadCompetitionDataAsync(firstComp.Id);

            }

        }



        SetupJudges(_currentCompetition?.Type ?? CompetitionType.Sanda);



        var knockouts = (await _drawKnockoutService.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();



        var orders = (await _drawOrderService.GetAllAsync("Draw.Category.AgeRange", "Competitor.Country"))?.ToList();



        var pools = (await _drawPoolsModel.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();



        var allDraws = new List<IDrawModel>();



        if (CompetitionId > 0)

        {

            if (knockouts != null) allDraws.AddRange(knockouts.Where(k => k.Draw?.Category?.CompetitionId == CompetitionId));

            if (orders != null) allDraws.AddRange(orders.Where(o => o.Draw?.Category?.CompetitionId == CompetitionId));

            if (pools != null) allDraws.AddRange(pools.Where(p => p.Draw?.Category?.CompetitionId == CompetitionId));

        }

        else

        {

            if (knockouts != null) allDraws.AddRange(knockouts);

            if (orders != null) allDraws.AddRange(orders);

            if (pools != null) allDraws.AddRange(pools);

        }



        var sortedDraws = allDraws.OrderBy(d => d.GlobalOrder).ToList();



        TotalMatches = sortedDraws.Count;

        var firstUnfinished = sortedDraws.FirstOrDefault(d => !d.IsFinished);

        CurrentDraw = firstUnfinished;



        RoundResults.Clear();
        if (CurrentDraw != null)
        {
            CurrentMatchNumber = sortedDraws.IndexOf(CurrentDraw) + 1;
            
            if (!string.IsNullOrEmpty(CurrentDraw.RoundResultsJson))
            {
                try
                {
                    var results = JsonSerializer.Deserialize<List<RoundResultModel>>(CurrentDraw.RoundResultsJson, _jsonOptions);
                    if (results != null)
                    {
                        foreach (var r in results)
                        {
                            RoundResults.Add(r);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize RoundResultsJson");
                }
            }
        }



        var flattenedList = new List<object>();

        string? lastCategoryName = null;



        foreach (var draw in sortedDraws.Where(d => !d.IsFinished))

        {

            var currentCategoryName = draw.Draw?.Category?.Name ?? "N/A";

            if (currentCategoryName != lastCategoryName)

            {

                flattenedList.Add(currentCategoryName);

                lastCategoryName = currentCategoryName;

            }

            flattenedList.Add(draw);

        }



        Draws = new System.Collections.ObjectModel.ObservableCollection<object>(flattenedList);



        await BroadcastMatchInfo();

        UpdateCurrentRoundVotes();

    }



    private async Task SaveRefereeData(JudgeModel judge, int? competitorId, double score, bool isCorrection = false, MvtTimeCodeModel? code = null)

    {

        if (CurrentDraw == null) return;



        if (code != null)

        {

            code.Score = score;

        }



        var data = new RefereeDataModel

        {

            Date = DateTime.Now,

            Referee = judge.Name,

            Score = score,

            Data = (code != null) ? JsonSerializer.Serialize(code) : String.Empty,

            IsCorrection = isCorrection,

            CompetitorId = competitorId,

            Round = CurrentDraw.CurrentRound

        };



        if (CurrentDraw is DrawKnockoutModel k) data.DrawKnockoutId = k.Id;

        else if (CurrentDraw is DrawOrderModel o) data.DrawOrderId = o.Id;

        else if (CurrentDraw is DrawPoolsModel p) data.DrawPoolsId = p.Id;



        await _refereeDataRepository.AddAsync(data);

    }



    private void StopTimer()

    {

        _timer?.Stop();

        IsTimerRunning = false;

    }

    #endregion



    #region Commands Timer

    /// <summary>

    /// Commande permettant de démarrer le timer

    /// </summary>

    /// <returns></returns>

    [RelayCommand]

    private async Task StartTimer()

    {

        if (!IsTimerRunning && _timeLeft.TotalSeconds > 0)

        {

            _timer?.Start();

            IsTimerRunning = true;

            await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_START);

        }

    }



    /// <summary>

    /// Commande permettant de mettre en pause le timer

    /// </summary>

    /// <returns></returns>

    [RelayCommand]

    private async Task PauseTimer()

    {

        StopTimer();

        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_PAUSE);

    }



    /// <summary>

    /// Commande permettant de réinitialiser le timer

    /// </summary>

    /// <returns></returns>

    [RelayCommand]

    private async Task ResetTimer()

    {

        StopTimer();

        _timeLeft = TimeSpan.FromMinutes(2); // Default reset

        OnPropertyChanged(nameof(TimeLeftDisplay));

        await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");

    }



    /// <summary>

    /// Commande permettant de définir un temps pour le timer

    /// </summary>

    /// <returns></returns>

    [RelayCommand]

    private async Task SetTimer()

    {

        string result = await Shell.Current.DisplayActionSheetAsync("Définir le temps", "Annuler", null, "1:00", "1:30", "2:00", "3:00", "5:00");

        if (result != null && result != "Annuler")

        {

            StopTimer();

            if (TimeSpan.TryParseExact(result, @"m\:ss", null, out var newTime))

            {

                _timeLeft = newTime;

                OnPropertyChanged(nameof(TimeLeftDisplay));

                await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");

            }

        }

    }

    #endregion



    #region Commands Server

    /// <summary>

    /// Commande permettant de démarrer le serveur

    /// </summary>

    /// <returns></returns>

    [RelayCommand]

    private async Task StartServer()

    {

        var parameters = new Dictionary<string, object?>();

        parameters.Add("Competition", _currentCompetition);



        var result = await _popupService.ShowPopupAsync<ServerSetupPopupViewModel, ServerSetupResult>(Shell.Current, shellParameters: parameters);



        if (result.Result != null)

        {

            ServerName = result.Result.Name;

            Title = $"{ServerName} - {result.Result.Description}";

            if (await _bluetoothServer.StartAdvertising("BluetoothAppService", ServerName))

            {

                IsServerStarted = true;

            }

        }

    }



    /// <summary>

    /// Commande permettant d'arrêter le serveur

    /// </summary>

    /// <returns></returns>

    [RelayCommand]
    private async Task StopServer()
    {
        await _bluetoothServer.StopAdvertising();
        IsServerStarted = false;
    }

    [RelayCommand]
    private async Task SetWinner(string color)
    {
        if (CurrentDraw == null) return;

        // Optionally, add the final round result before setting the winner
        int redVotes = (int)Judges.Sum(j => j.RedPoints);
        int blueVotes = (int)Judges.Sum(j => j.BluePoints);
        if (redVotes > 0 || blueVotes > 0 || CurrentDraw.Type == RoundType.Knockouts || CurrentDraw.Type == RoundType.Pools)
        {
            string roundWinner = color;
            RoundResults.Add(new RoundResultModel
            {
                RoundNumber = CurrentDraw.CurrentRound,
                RedVotes = redVotes,
                BlueVotes = blueVotes,
                WinnerColor = roundWinner
            });
            CurrentDraw.RoundResultsJson = JsonSerializer.Serialize(RoundResults, _jsonOptions);
            
            // Check if match is finished (best of 3 logic for Sanda)
            if (CurrentDraw.Type != RoundType.Order)
            {
                int redWins = RoundResults.Count(r => r.WinnerColor == "Red");
                int blueWins = RoundResults.Count(r => r.WinnerColor == "Blue");
                
                if (redWins < 2 && blueWins < 2 && CurrentDraw.CurrentRound < 3)
                {
                    CurrentDraw.CurrentRound++;
                    if (CurrentDraw is DrawKnockoutModel k1) await _drawKnockoutService.UpdateAsync(k1);
                    else if (CurrentDraw is DrawPoolsModel p1) await _drawPoolsModel.UpdateAsync(p1);
                    
                    ResetJudgeScores();
                    await ResetTimer();
                    await BroadcastMatchInfo();
                    return; // Continue match
                }
            }
        }

        CompetitorModel? winner = null;
        CompetitorModel? looser = null;

        if (CurrentDraw is DrawKnockoutModel knockout)
        {
            winner = color == "Red" ? knockout.Competitor1 : knockout.Competitor2;
            looser = color == "Red" ? knockout.Competitor2 : knockout.Competitor1;

            if (winner != null)
            {
                knockout.WinnerId = winner.Id;
                knockout.LooserId = looser?.Id;
                knockout.IsFinished = true;
                await _drawKnockoutService.UpdateAsync(knockout);

                // Propagation logic for Knockouts
                var allKnockouts = (await _drawKnockoutService.GetAllAsync())?.Where(k => k.DrawId == knockout.DrawId).OrderBy(k => k.Order).ToList();
                if (allKnockouts != null && allKnockouts.Count > 0)
                {
                    int m = allKnockouts.Max(k => k.Order) + 1;
                    int currentOrder = knockout.Order;
                    int roundStart = 0;
                    int matchesInRound = m / 2;

                    while (matchesInRound > 0)
                    {
                        if (currentOrder > roundStart && currentOrder <= roundStart + matchesInRound)
                        {
                            int localIndex = currentOrder - roundStart;
                            int nextRoundStart = roundStart + matchesInRound;
                            int nextMatchesInRound = matchesInRound / 2;

                            if (nextMatchesInRound > 0)
                            {
                                int nextMatchOrder = nextRoundStart + (localIndex + 1) / 2;
                                var nextMatch = allKnockouts.FirstOrDefault(k => k.Order == nextMatchOrder);
                                if (nextMatch != null)
                                {
                                    if (localIndex % 2 != 0) nextMatch.Competitor1Id = winner.Id;
                                    else nextMatch.Competitor2Id = winner.Id;
                                    await _drawKnockoutService.UpdateAsync(nextMatch);
                                }
                            }
                            break;
                        }
                        roundStart += matchesInRound;
                        matchesInRound /= 2;
                    }
                }
            }
        }
        else if (CurrentDraw is DrawPoolsModel pool)
        {
            winner = color == "Red" ? pool.Competitor1 : pool.Competitor2;
            looser = color == "Red" ? pool.Competitor2 : pool.Competitor1;

            if (winner != null)
            {
                pool.WinnerId = winner.Id;
                pool.LooserId = looser?.Id;
                pool.IsFinished = true;
                await _drawPoolsModel.UpdateAsync(pool);
            }
        }
        else if (CurrentDraw is DrawOrderModel order)
        {
            order.IsFinished = true;
            await _drawOrderService.UpdateAsync(order);
            winner = order.Competitor; // Just to trigger next
        }

        if (winner != null)
        {
            ResetJudgeScores();
            await ResetTimer();
            await Init();
        }
    }



    [RelayCommand]

    private async Task CheckBluetoothAvailability()

    {

        if (!await _bluetoothService.RequestBluetoothPermissions())

        {

            BluetoothAvailable = false;

        }

        else

        {

            BluetoothAvailable = true;

        }

    }



    [RelayCommand]

    private async Task OpenJudgePopup(JudgeModel? judge)

    {

        if (judge != null)

        {

            var parameters = new Dictionary<string, object> { { "Judge", judge } };



            var result = await _popupService.ShowPopupAsync<JudgePointsPopupViewModel, JudgeModel>(Shell.Current, shellParameters: parameters);



            if (result.Result != null)

            {

                judge?.RedPoints = result.Result.RedPoints;

                judge?.BluePoints = result.Result.BluePoints;

                UpdateCurrentRoundVotes();

            }

        }

    }



    [RelayCommand]

    private async Task AddRedPoint(JudgeModel judge)

    {

        judge.RedPoints++;

        OnPropertyChanged(nameof(Judges));

        UpdateCurrentRoundVotes();



        var competitor = GetCompetitor(CurrentDraw, true);

        if (competitor != null) await SaveRefereeData(judge, competitor.Id, 1, true);

    }



    [RelayCommand]

    private async Task RemoveRedPoint(JudgeModel judge)

    {

        if (judge.RedPoints > 0)

        {

            judge.RedPoints--;

            OnPropertyChanged(nameof(Judges));

            UpdateCurrentRoundVotes();



            var competitor = GetCompetitor(CurrentDraw, true);

            if (competitor != null) await SaveRefereeData(judge, competitor.Id, -1, true);

        }

    }



    [RelayCommand]

    private async Task AddBluePoint(JudgeModel judge)

    {

        judge.BluePoints++;

        OnPropertyChanged(nameof(Judges));

        UpdateCurrentRoundVotes();



        var competitor = GetCompetitor(CurrentDraw, false);

        if (competitor != null) await SaveRefereeData(judge, competitor.Id, 1, true);

    }



    [RelayCommand]

    private async Task RemoveBluePoint(JudgeModel judge)

    {

        if (judge.BluePoints > 0)

        {

            judge.BluePoints--;

            OnPropertyChanged(nameof(Judges));

            UpdateCurrentRoundVotes();



            var competitor = GetCompetitor(CurrentDraw, false);

            if (competitor != null) await SaveRefereeData(judge, competitor.Id, -1, true);

        }

    }

    #endregion



    #region SideSheet logic

    private bool _isSideSheetOpen;





    public bool IsSideSheetOpen

    {

        get => _isSideSheetOpen;

        set => SetProperty(ref _isSideSheetOpen, value);

    }



    [RelayCommand]

    private void ToggleSideSheet()

    {

        IsSideSheetOpen = !IsSideSheetOpen;

    }



    [RelayCommand]

    private void CloseSideSheet()

    {

        IsSideSheetOpen = false;

    }



    [RelayCommand]

    private async Task GoToDraws()

    {

        IsSideSheetOpen = false;

        await Shell.Current.GoToAsync($"DrawsManagementPage?CompetitionId={CompetitionId}");

    }

    #endregion

}