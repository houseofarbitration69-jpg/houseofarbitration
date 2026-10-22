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

[QueryProperty(nameof(CompetitionId), Constants.Navigation.Parameter.COMPETITION_ID)]

public partial class ServerViewModel : BaseViewModel
{
    #region Services
    /// <summary>
    /// Service de gestion des alertes
    /// </summary>
    private readonly IAlertService _alertService;

    /// <summary>
    /// Service de gestion du bluetooth
    /// </summary>
    private readonly IBluetoothService _bluetoothService;

    /// <summary>
    /// Server bluetooth
    /// </summary>
    private readonly IBluetoothServer _bluetoothServer;

    /// <summary>
    /// Service d'accès aux données pour les compétitions
    /// </summary>
    private readonly IRepository<CompetitionModel> _competitionRepository;

    /// <summary>
    /// Service d'accès aux données pour les tirages eliminatoire
    /// </summary>
    private readonly IRepository<DrawKnockoutModel> _drawKnockoutService;

    /// <summary>
    /// Service d'accès aux données pour les tirages ordonnée
    /// </summary>
    private readonly IRepository<DrawOrderModel> _drawOrderService;

    /// <summary>
    /// Service d'accès aux données pour les tirages par poule
    /// </summary>
    private readonly IRepository<DrawPoolsModel> _drawPoolsModel;

    /// <summary>
    /// Service d'accès aux données pour les informations des juges
    /// </summary>
    private readonly IRepository<RefereeDataModel> _refereeDataRepository;
    #endregion

    #region Attributs
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
    private System.Collections.ObjectModel.ObservableCollection<JudgeGroupModel> _judgeGroups = new();
    private double _currentRedVotes;
    private double _currentBlueVotes;
    #endregion

    #region Properties
    public string TimeLeftDisplay => _timeLeft.ToString(@"mm\:ss");

    /// <summary>
    /// Obtient ou définit si le bluetooth est disponible
    /// </summary>
    public bool BluetoothAvailable
    {
        get => _bluetoothAvailable;
        set => SetProperty(ref _bluetoothAvailable, value);
    }

    /// <summary>
    /// Obtient ou définit le nom du serveur
    /// </summary>
    public string ServerName
    {
        get => _serverName;
        set => SetProperty(ref _serverName, value);
    }

    /// <summary>
    /// Obtient ou définit la liste des tirages
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<object>? Draws
    {
        get => _draws;
        set => SetProperty(ref _draws, value);
    }

    /// <summary>
    /// Obtient ou définit la liste des juges
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<JudgeModel> Judges
    {
        get => _judges;
        set => SetProperty(ref _judges, value);
    }

    /// <summary>
    /// Obtient ou définit la liste des groupes des juges
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<JudgeGroupModel> JudgeGroups
    {
        get => _judgeGroups;
        set => SetProperty(ref _judgeGroups, value);
    }

    /// <summary>
    /// Obtient ou définit la liste des rounds
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<RoundResultModel> RoundResults
    {
        get => _roundResults;
        set => SetProperty(ref _roundResults, value);
    }

    /// <summary>
    /// Obtient ou définit si le timer tourne
    /// </summary>
    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        set => SetProperty(ref _isTimerRunning, value);
    }

    /// <summary>
    /// Obtient ou définit si le serveur tourne
    /// </summary>
    public bool IsServerStarted
    {
        get => _isServerStarted;
        set => SetProperty(ref _isServerStarted, value);
    }

    /// <summary>
    /// Obtient ou définit le tirage courant
    /// </summary>
    public IDrawModel? CurrentDraw
    {
        get => _currentDraw;
        set => SetProperty(ref _currentDraw, value);
    }

    /// <summary>
    /// Obtient ou définit l'identifiant de la compétition
    /// </summary>
    public int CompetitionId
    {
        get => _competitionId;
        set => SetProperty(ref _competitionId, value);
    }

    /// <summary>
    /// Obtient ou définit le nombre total de matche
    /// </summary>
    public int TotalMatches
    {
        get => _totalMatches;
        set => SetProperty(ref _totalMatches, value);
    }

    /// <summary>
    /// Obtient ou définit le numéro du match courant
    /// </summary>
    public int CurrentMatchNumber
    {
        get => _currentMatchNumber;
        set => SetProperty(ref _currentMatchNumber, value);
    }

    /// <summary>
    /// Obtient ou définit les votes pour le combattant rouge
    /// </summary>
    public double CurrentRedVotes
    {
        get => _currentRedVotes;
        set => SetProperty(ref _currentRedVotes, value);
    }

    /// <summary>
    /// Obtient ou définit les votes pour le combattant bleu
    /// </summary>
    public double CurrentBlueVotes
    {
        get => _currentBlueVotes;
        set => SetProperty(ref _currentBlueVotes, value);
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Constructeur permettant d'initialiser les services
    /// </summary>
    /// <param name="logger">Service de log</param>
    /// <param name="resourceProvider">Service de traduction</param>
    /// <param name="popupService">Service de gestion des popups</param>
    /// <param name="alertService">Service de gestion des alertes</param>
    /// <param name="bluetoothService">Service pour le bluetooth</param>
    /// <param name="bluetoothServer">Service du serveur bluetooth</param>
    /// <param name="competitionRepository">Servide d'accès aux données pour les compétitions</param>
    /// <param name="drawKnockoutService">Service d'accès aux données pour les tirages eliminatoire</param>
    /// <param name="drawOrderService">Service d'accès aux données pour les tirages par ordre</param>
    /// <param name="drawPoolsService">Service d'accès aux données pour les tirages par poule</param>
    /// <param name="refereeDataRepository">Servide d'accès aux données pour les données des juges</param>
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
    /// <summary>
    /// Evenement lorsqu'un device se connect
    /// </summary>
    /// <param name="sender">L'objet emetteur</param>
    /// <param name="clientId">Identifiant du device</param>
    private async void OnDeviceConnected(object? sender, string clientId)
    {
        await SendCompetitionDataToClientAsync(clientId);
    }

    /// <summary>
    /// Evenement lorsqu'un device se déconnect
    /// </summary>
    /// <param name="sender">L'objet emetteur</param>
    /// <param name="clientId">Idenifiant du device</param>
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

    /// <summary>
    /// Evenement lors de la réception d'un message
    /// </summary>
    /// <param name="sender">L'objet emetteur</param>
    /// <param name="args"></param>
    private async void OnMessageReceived(object? sender, (string ClientId, string Message) args)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (args.Message == Constants.Message.PING)
            {
                try
                {
                    await _bluetoothServer.SendToClientAsync(Constants.Message.PONG, args.ClientId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error replying PONG to client {ClientId}", args.ClientId);
                }

                return;
            }

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

                    // Restore codes from repository if server in-memory codes are empty
                    if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order && judge.Codes.Count == 0)
                    {
                        try
                        {
                            var allData = await _refereeDataRepository.GetAllAsync();
                            if (allData != null)
                            {
                                var judgeHistory = allData
                                    .Where(d => d.Referee == judge.Name &&
                                        ((CurrentDraw is DrawOrderModel o && d.DrawOrderId == o.Id) ||
                                         (CurrentDraw is DrawKnockoutModel k && d.DrawKnockoutId == k.Id) ||
                                         (CurrentDraw is DrawPoolsModel p && d.DrawPoolsId == p.Id)))
                                    .OrderBy(d => d.Date)
                                    .ToList();

                                foreach (var item in judgeHistory)
                                {
                                    if (!string.IsNullOrEmpty(item.Data))
                                    {
                                        var mvtCode = JsonSerializer.Deserialize<MvtTimeCodeModel>(item.Data, _jsonOptions);
                                        if (mvtCode != null)
                                        {
                                            judge.Codes.Insert(0, mvtCode);
                                        }
                                    }
                                    if (item.Score > 0)
                                    {
                                        judge.Score = item.Score;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error restoring judge history from repository for client {ClientId}", args.ClientId);
                        }
                    }

                    await SendJudgeStateAsync(judge, args.ClientId);
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
                            if (scoreData.Code != null)
                            {
                                judge.Codes.Insert(0, scoreData.Code);
                            }
                            else if (judge.Codes.Count > 0)
                            {
                                judge.Codes.RemoveAt(0);
                            }
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
            else if (args.Message.StartsWith(Constants.Message.JUDGE_SYNC_STATE))
            {
                try
                {
                    var json = args.Message.Substring(Constants.Message.JUDGE_SYNC_STATE.Length);
                    var clientState = JsonSerializer.Deserialize<JudgeStateModel>(json, _jsonOptions);
                    if (clientState != null)
                    {
                        JudgeModel? judge = null;
                        if (!string.IsNullOrEmpty(clientState.Group))
                        {
                            judge = Judges.FirstOrDefault(j => j.Group == clientState.Group && j.Number == clientState.Number);
                        }
                        else
                        {
                            judge = Judges.FirstOrDefault(j => j.Number == clientState.Number)
                                 ?? Judges.FirstOrDefault(j => j.Name == clientState.Name);
                        }

                        if (judge != null)
                        {
                            judge.IsConnected = true;
                            _clientJudgeMapping[args.ClientId] = judge;

                            judge.Score = clientState.Score;
                            judge.RedPoints = clientState.RedPoints;
                            judge.BluePoints = clientState.BluePoints;

                            if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
                            {
                                judge.Codes.Clear();
                                if (clientState.Codes != null)
                                {
                                    foreach (var c in clientState.Codes)
                                    {
                                        judge.Codes.Add(c);
                                    }
                                }

                                var competitor = GetCompetitor(CurrentDraw, true);
                                if (competitor != null && clientState.Codes != null)
                                {
                                    var existingData = await _refereeDataRepository.GetAllAsync();
                                    var savedDates = existingData?
                                        .Where(d => d.Referee == judge.Name &&
                                            ((CurrentDraw is DrawOrderModel o && d.DrawOrderId == o.Id) ||
                                             (CurrentDraw is DrawKnockoutModel k && d.DrawKnockoutId == k.Id) ||
                                             (CurrentDraw is DrawPoolsModel p && d.DrawPoolsId == p.Id)))
                                        .Select(d => d.Date)
                                        .ToHashSet() ?? new HashSet<DateTime>();

                                    foreach (var code in clientState.Codes)
                                    {
                                        if (!savedDates.Contains(code.Date))
                                        {
                                            await SaveRefereeData(judge, competitor.Id, code.Score, false, code);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                UpdateCurrentRoundVotes();
                            }

                            OnPropertyChanged(nameof(Judges));

                            // Send sync ACK
                            await _bluetoothServer.SendToClientAsync(Constants.Message.JUDGE_SYNC_ACK, args.ClientId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing judge sync state from client {ClientId}", args.ClientId);
                }
            }
        });
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

    public override async Task OnDisappearing()
    {
        if (IsServerStarted)
        {
            try
            {
                await _bluetoothServer.SendToAllAsync(Constants.Message.SERVER_STOPPED);
                await Task.Delay(150);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying clients on server disappearing");
            }

            await _bluetoothServer.StopAdvertising();
            IsServerStarted = false;
        }

        _bluetoothServer.DeviceConnected -= OnDeviceConnected;
        _bluetoothServer.DeviceDisconnected -= OnDeviceDisconnected;
        _bluetoothServer.MessageReceived -= OnMessageReceived;

        await base.OnDisappearing();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 
    /// </summary>
    /// <param name="judge"></param>
    /// <param name="clientId"></param>
    /// <returns></returns>
    private async Task SendJudgeStateAsync(JudgeModel judge, string? clientId = null)
    {
        try
        {
            if (clientId == null)
            {
                clientId = _clientJudgeMapping.FirstOrDefault(x => x.Value == judge).Key;
            }

            if (string.IsNullOrEmpty(clientId)) return;

            var judgeState = new JudgeStateModel
            {
                Group = judge.Group,
                Number = judge.Number,
                Name = judge.Name,
                Score = judge.Score,
                RedPoints = judge.RedPoints,
                BluePoints = judge.BluePoints,
                Codes = judge.Codes.ToList()
            };
        
            var stateJson = JsonSerializer.Serialize(judgeState, _jsonOptions);
            await _bluetoothServer.SendToClientAsync($"{Constants.Message.JUDGE_STATE}{stateJson}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending judge state for judge {JudgeName}", judge.Name);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="draw"></param>
    /// <param name="red"></param>
    /// <returns></returns>
    private CompetitorModel? GetCompetitor(IDrawModel? draw, bool red)
    {
        if (draw is DrawKnockoutModel k)
        {
            return red ? k.Competitor1 : k.Competitor2;
        }

        if (draw is DrawPoolsModel p)
        {
            return red ? p.Competitor1 : p.Competitor2;
        }

        if (draw is DrawOrderModel o)
        {
            return o.Competitor;
        }

        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    private void ResetJudgeScores()
    {
        foreach (var judge in Judges)
        {
            judge.RedPoints = 0;
            judge.BluePoints = 0;
            judge.Codes.Clear();

            if (judge.Group == "A")
            {
                judge.Score = 5.0;
            }
            else if (judge.Group == "B")
            {
                judge.Score = 3.0;
            }
            else if (judge.Group == "C")
            {
                judge.Score = 2.0;
            }
            else
            {
                judge.Score = 0.0;
            }
        }

        OnPropertyChanged(nameof(Judges));
        UpdateCurrentRoundVotes();
    }

    /// <summary>
    /// 
    /// </summary>
    private void UpdateCurrentRoundVotes()
    {
        CurrentRedVotes = Judges.Sum(j => j.RedPoints);
        CurrentBlueVotes = Judges.Sum(j => j.BluePoints);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="competitionType"></param>
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="competitionId"></param>
    /// <returns></returns>
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
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
            if (knockouts != null)
            {
                allDraws.AddRange(knockouts.Where(k => k.Draw?.Category?.CompetitionId == CompetitionId));
            }

            if (orders != null)
            {
                allDraws.AddRange(orders.Where(o => o.Draw?.Category?.CompetitionId == CompetitionId));
            }

            if (pools != null)
            {
                allDraws.AddRange(pools.Where(p => p.Draw?.Category?.CompetitionId == CompetitionId));
            }
        }
        else
        {
            if (knockouts != null)
            {
                allDraws.AddRange(knockouts);
            }

            if (orders != null)
            {
                allDraws.AddRange(orders);
            }

            if (pools != null)
            {
                allDraws.AddRange(pools);
            }
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="judge"></param>
    /// <param name="competitorId"></param>
    /// <param name="score"></param>
    /// <param name="isCorrection"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    private async Task SaveRefereeData(JudgeModel judge, int? competitorId, double score, bool isCorrection = false, MvtTimeCodeModel? code = null)
    {
        if (CurrentDraw == null)
        {
            return;
        }

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

        if (CurrentDraw is DrawKnockoutModel k)
        {
            data.DrawKnockoutId = k.Id;
        }
        else if (CurrentDraw is DrawOrderModel o)
        {
            data.DrawOrderId = o.Id;
        }
        else if (CurrentDraw is DrawPoolsModel p)
        {
            data.DrawPoolsId = p.Id;
        }

        await _refereeDataRepository.AddAsync(data);
    }

    /// <summary>
    /// 
    /// </summary>
    private void StopTimer()
    {
        _timer?.Stop();
        IsTimerRunning = false;
    }
    #endregion

    #region Commands
    /// <summary>
    /// Commande permettant de broadcaster les informations du match
    /// </summary>
    /// <returns></returns>
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
        if (_currentCompetition != null)
        {
            var parameters = new Dictionary<string, object>();
            parameters.Add(Constants.Navigation.Parameter.COMPETITION, _currentCompetition);

            var result = await _popupService.ShowPopupAsync<ServerSetupPopupViewModel, ServerSetupResult>(
                Shell.Current,
                options: new PopupOptions() { Shadow = null },
                shellParameters: parameters
            );

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
    }


    /// <summary>
    /// Commande permettant d'arrêter le serveur
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task StopServer()
    {
        try
        {
            await _bluetoothServer.SendToAllAsync(Constants.Message.SERVER_STOPPED);
            await Task.Delay(150);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying clients on StopServer");
        }

        await _bluetoothServer.StopAdvertising();
        IsServerStarted = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task SetWinner(string color)
    {
        if (CurrentDraw == null)
        {
            return;
        }

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
                    if (CurrentDraw is DrawKnockoutModel k1)
                    {
                        await _drawKnockoutService.UpdateAsync(k1);
                    }
                    else if (CurrentDraw is DrawPoolsModel p1)
                    {
                        await _drawPoolsModel.UpdateAsync(p1);
                    }

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

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
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


    /// <summary>
    /// 
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenJudgePopup(JudgeModel? judge)
    {
        if (judge != null)
        {
            var parameters = new Dictionary<string, object> { { "Judge", judge } };

            var result = await _popupService.ShowPopupAsync<JudgePointsPopupViewModel, JudgeModel>(
                Shell.Current, 
                options: new PopupOptions() { Shadow = null },
                shellParameters: parameters
            );

            if (result.Result != null && judge != null)
            {
                judge.RedPoints = result.Result.RedPoints;
                judge.BluePoints = result.Result.BluePoints;

                UpdateCurrentRoundVotes();

                await SendJudgeStateAsync(judge);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task AddRedPoint(JudgeModel judge)
    {
        judge.RedPoints++;

        OnPropertyChanged(nameof(Judges));

        UpdateCurrentRoundVotes();

        await SendJudgeStateAsync(judge);

        var competitor = GetCompetitor(CurrentDraw, true);

        if (competitor != null)
        {
            await SaveRefereeData(judge, competitor.Id, 1, true);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task RemoveRedPoint(JudgeModel judge)
    {
        if (judge.RedPoints > 0)
        {
            judge.RedPoints--;

            OnPropertyChanged(nameof(Judges));

            UpdateCurrentRoundVotes();

            await SendJudgeStateAsync(judge);

            var competitor = GetCompetitor(CurrentDraw, true);

            if (competitor != null)
            {
                await SaveRefereeData(judge, competitor.Id, -1, true);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task AddBluePoint(JudgeModel judge)
    {
        judge.BluePoints++;

        OnPropertyChanged(nameof(Judges));

        UpdateCurrentRoundVotes();

        await SendJudgeStateAsync(judge);

        var competitor = GetCompetitor(CurrentDraw, false);

        if (competitor != null)
        {
            await SaveRefereeData(judge, competitor.Id, 1, true);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task RemoveBluePoint(JudgeModel judge)
    {
        if (judge.BluePoints > 0)
        {
            judge.BluePoints--;

            OnPropertyChanged(nameof(Judges));

            UpdateCurrentRoundVotes();

            await SendJudgeStateAsync(judge);

            var competitor = GetCompetitor(CurrentDraw, false);

            if (competitor != null)
            {
                await SaveRefereeData(judge, competitor.Id, -1, true);
            }
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

    /// <summary>
    /// 
    /// </summary>
    [RelayCommand]
    private void ToggleSideSheet()
    {
        IsSideSheetOpen = !IsSideSheetOpen;
    }

    /// <summary>
    /// 
    /// </summary>
    [RelayCommand]
    private void CloseSideSheet()
    {
        IsSideSheetOpen = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task GoToDraws()
    {
        IsSideSheetOpen = false;

        await Shell.Current.GoToAsync($"DrawsManagementPage?CompetitionId={CompetitionId}");
    }
    #endregion
}