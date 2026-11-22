#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
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
    private TimeSpan _totalRoundTime = TimeSpan.FromMinutes(2);
    private bool _isTimerRunning;
    private bool _isServerStarted;
    private IDispatcherTimer? _timer;
    private int _competitionId;
    private int _totalMatches;
    private int _currentMatchNumber;
    private CompetitionModel? _currentCompetition;
    private readonly ConcurrentDictionary<string, JudgeModel> _clientJudgeMapping = new();
    private readonly SemaphoreSlim _dbSemaphore = new(1, 1);
    private readonly object _judgesLock = new();
    private List<JudgeModel> _judgesSnapshot = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _lastSentServerNameToClient = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _lastSentCompToClient = new();
    private System.Collections.ObjectModel.ObservableCollection<JudgeGroupModel> _judgeGroups = new();
    private double _currentRedVotes;
    private double _currentBlueVotes;
    private bool _isNavigatingBack;
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
        set
        {
            if (SetProperty(ref _currentDraw, value))
            {
                OnPropertyChanged(nameof(CurrentMatchStageName));
            }
        }
    }

    /// <summary>
    /// Obtient le nom de la phase/étape du match en cours (ex: "Finale", "Demi-finale 1", "Quart de finale 2")
    /// </summary>
    public string CurrentMatchStageName
    {
        get
        {
            if (CurrentDraw is DrawKnockoutModel k)
            {
                return k.StageName;
            }
            if (CurrentDraw is DrawPoolsModel p)
            {
                return $"Poule - Match #{p.Order}";
            }
            return string.Empty;
        }
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
        lock (_judgesLock)
        {
            _judgesSnapshot = Judges.ToList();
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

                    OnPropertyChanged(nameof(TimeLeftDisplay));

                    // Resynchronisation périodique toutes les 5 secondes
                    if ((int)_timeLeft.TotalSeconds % 5 == 0)
                    {
                        string syncMsg = $"{Constants.Message.TIMER_SET}{TimeLeftDisplay}";
                        _ = Task.Run(async () =>
                        {
                            await _bluetoothServer.SendToAllAsync(syncMsg);
                        });
                    }
                }
                else
                {
                    _ = StopTimer();

                    _ = Task.Run(async () =>
                    {
                        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_STOP);
                    });
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
    private void OnDeviceConnected(object? sender, string clientId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await SendCompetitionDataToClientAsync(clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending initial competition data to client {ClientId}", clientId);
            }
        });
    }

    /// <summary>
    /// Evenement lorsqu'un device se déconnect
    /// </summary>
    /// <param name="sender">L'objet emetteur</param>
    /// <param name="clientId">Idenifiant du device</param>
    private void OnDeviceDisconnected(object? sender, string clientId)
    {
        var matchingKeys = _clientJudgeMapping
            .Where(kvp => kvp.Key == clientId || kvp.Key.StartsWith(clientId + "#"))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in matchingKeys)
        {
            if (_clientJudgeMapping.TryRemove(key, out var judge))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    judge.IsConnected = false;
                });
            }
        }
    }

    /// <summary>
    /// Evenement lors de la réception d'un message
    /// </summary>
    /// <param name="sender">L'objet emetteur</param>
    /// <param name="args"></param>
    private async void OnMessageReceived(object? sender, (string ClientId, string Message) args)
    {
        // Répondre au PING directement, sans passer par le thread UI :
        // le PONG n'a pas besoin de toucher l'UI et évite ainsi de saturer
        // MainThread.BeginInvokeOnMainThread avec des lambdas async fréquentes.
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

        // Snapshot thread-safe de Judges sans solliciter le thread UI
        List<JudgeModel> judgesSnapshot;
        lock (_judgesLock)
        {
            judgesSnapshot = _judgesSnapshot;
        }

        if (args.Message == Constants.Message.GET_COMPETITION)
        {
            if (_lastSentCompToClient.TryGetValue(args.ClientId, out var lastTime) && (DateTime.UtcNow - lastTime).TotalSeconds < 2)
            {
                return;
            }
            await SendCompetitionDataToClientAsync(args.ClientId);
            return;
        }
        else if (args.Message.StartsWith(Constants.Message.GET_MATCH))
        {
            // Envoi du match courant
            if (CurrentDraw != null)
            {
                try
                {
                    var matchData = CreateMatchInfoData(CurrentDraw);
                    var json = JsonSerializer.Serialize(matchData, _jsonOptions);
                    await _bluetoothServer.SendToClientAsync($"{Constants.Message.MATCH_INFO}{json}", args.ClientId);
                    await _bluetoothServer.SendToClientAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}", args.ClientId);
                    if (IsTimerRunning)
                    {
                        await _bluetoothServer.SendToClientAsync(Constants.Message.TIMER_START, args.ClientId);
                    }
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
        else if (args.Message.StartsWith(Constants.Message.JUDGE_POSITION))
        {
            var posStr = args.Message.Substring(Constants.Message.JUDGE_POSITION.Length);

            // Support du session ID pour permettre plusieurs juges sur un même PC (ex: "1#instA")
            string sessionKey = args.ClientId;
            if (posStr.Contains('#'))
            {
                var segs = posStr.Split('#', 2);
                posStr = segs[0];
                sessionKey = $"{args.ClientId}#{segs[1]}";
            }

            JudgeModel? judge = null;
            var parts = posStr.Split('_');

            if (parts.Length == 2 && int.TryParse(parts[1], out int num))
            {
                judge = judgesSnapshot.FirstOrDefault(j => j.Group == parts[0] && j.Number == num);
            }
            else if (int.TryParse(posStr, out int position))
            {
                judge = judgesSnapshot.FirstOrDefault(j => string.IsNullOrEmpty(j.Group) && j.Number == position)
                     ?? judgesSnapshot.FirstOrDefault(j => j.Number == position);
            }
            else
            {
                judge = judgesSnapshot.FirstOrDefault(j => j.Name == posStr);
            }

            if (judge != null)
            {
                // Remove previous mapping for this client session if any
                if (_clientJudgeMapping.TryGetValue(sessionKey, out var oldJudge) && oldJudge != judge)
                {
                    MainThread.BeginInvokeOnMainThread(() => oldJudge.IsConnected = false);
                }

                // Remove mapping for this judge if another client was using it
                var existingMapping = _clientJudgeMapping.FirstOrDefault(x => x.Value == judge);
                if (existingMapping.Key != null && existingMapping.Key != sessionKey)
                {
                    _clientJudgeMapping.TryRemove(existingMapping.Key, out _);
                }

                _clientJudgeMapping[sessionKey] = judge;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    judge.IsConnected = true;
                });

                // Envoi immédiat du match en cours au juge enregistré
                if (CurrentDraw != null)
                {
                    try
                    {
                        var matchData = CreateMatchInfoData(CurrentDraw);
                        var json = JsonSerializer.Serialize(matchData, _jsonOptions);
                        await _bluetoothServer.SendToClientAsync($"{Constants.Message.MATCH_INFO}{json}", args.ClientId);
                        await _bluetoothServer.SendToClientAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}", args.ClientId);
                        if (IsTimerRunning)
                        {
                            await _bluetoothServer.SendToClientAsync(Constants.Message.TIMER_START, args.ClientId);
                        }
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
                        await _dbSemaphore.WaitAsync();
                        IReadOnlyList<RefereeDataModel>? allData;
                        try
                        {
                            allData = await _refereeDataRepository.GetAllAsync();
                        }
                        finally
                        {
                            _dbSemaphore.Release();
                        }

                        if (allData != null)
                        {
                            var judgeHistory = allData
                                .Where(d => d.Referee == judge.Name &&
                                    ((CurrentDraw is DrawOrderModel o && d.DrawOrderId == o.Id) ||
                                     (CurrentDraw is DrawKnockoutModel k && d.DrawKnockoutId == k.Id) ||
                                     (CurrentDraw is DrawPoolsModel p && d.DrawPoolsId == p.Id)))
                                .OrderBy(d => d.Date)
                                .ToList();

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
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
                            });
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
            var payload = args.Message.Substring(Constants.Message.JUDGE_DISCONNECT.Length).TrimStart(':');
            List<string> keysToRemove = new();

            if (!string.IsNullOrEmpty(payload))
            {
                string posPart = payload;
                string? instPart = null;
                if (payload.Contains('#'))
                {
                    var segs = payload.Split('#', 2);
                    posPart = segs[0];
                    instPart = segs[1];
                }

                if (!string.IsNullOrEmpty(instPart))
                {
                    string targetKey = $"{args.ClientId}#{instPart}";
                    if (_clientJudgeMapping.TryGetValue(targetKey, out var j))
                    {
                        MainThread.BeginInvokeOnMainThread(() => j.IsConnected = false);
                        keysToRemove.Add(targetKey);
                    }
                }

                if (keysToRemove.Count == 0 && !string.IsNullOrEmpty(posPart))
                {
                    foreach (var kvp in _clientJudgeMapping.Where(k => k.Key == args.ClientId || k.Key.StartsWith(args.ClientId + "#")))
                    {
                        var j = kvp.Value;
                        if (j.Name == posPart || j.Number.ToString() == posPart || $"{j.Group}_{j.Number}" == posPart)
                        {
                            MainThread.BeginInvokeOnMainThread(() => j.IsConnected = false);
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }
            }

            if (keysToRemove.Count == 0)
            {
                foreach (var kvp in _clientJudgeMapping.Where(k => k.Key == args.ClientId || k.Key.StartsWith(args.ClientId + "#")))
                {
                    var j = kvp.Value;
                    MainThread.BeginInvokeOnMainThread(() => j.IsConnected = false);
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var k in keysToRemove)
            {
                _clientJudgeMapping.TryRemove(k, out _);
            }
        }
        else if (args.Message.StartsWith(Constants.Message.JUDGE_SCORE))
        {
            bool isTaolu = _currentCompetition?.Type == CompetitionType.Taolu || CurrentDraw?.Type == RoundType.Order || CurrentDraw is DrawOrderModel;
            if (!isTaolu && (!IsTimerRunning || _timeLeft.TotalSeconds <= 0))
            {
                _logger.LogWarning("Score reçu du juge ({ClientId}) alors que le chronomètre est arrêté, en pause ou expiré. Ignoré.", args.ClientId);
                return;
            }

            try
            {
                var json = args.Message.Substring(Constants.Message.JUDGE_SCORE.Length);
                var scoreData = JsonSerializer.Deserialize<TransfertScoreModel>(json, _jsonOptions);

                JudgeModel? judge = null;
                // Recherche par position explicite (prioritaire pour multi-instances sur un même PC)
                if (!string.IsNullOrEmpty(scoreData?.JudgePosition))
                {
                    var parts = scoreData.JudgePosition.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int num))
                    {
                        judge = judgesSnapshot.FirstOrDefault(j => j.Group == parts[0] && j.Number == num);
                    }
                    else if (int.TryParse(scoreData.JudgePosition, out int posNum))
                    {
                        judge = judgesSnapshot.FirstOrDefault(j => string.IsNullOrEmpty(j.Group) && j.Number == posNum)
                             ?? judgesSnapshot.FirstOrDefault(j => j.Number == posNum);
                    }
                    else
                    {
                        judge = judgesSnapshot.FirstOrDefault(j => j.Name == scoreData.JudgePosition);
                    }
                }

                // Repli sur le mapping d'adresse client
                if (judge == null)
                {
                    _clientJudgeMapping.TryGetValue(args.ClientId, out judge);
                }

                if (judge != null && scoreData != null)
                {
                    if (CurrentDraw != null && CurrentDraw.Type != RoundType.Order)
                    {
                        var competitor1 = GetCompetitor(CurrentDraw, true);
                        var competitor2 = GetCompetitor(CurrentDraw, false);

                        bool isRed = (competitor1 != null && scoreData.CompetitorId == competitor1.Id) || scoreData.Side == Constants.Side.Red;
                        bool isBlue = (competitor2 != null && scoreData.CompetitorId == competitor2.Id) || scoreData.Side == Constants.Side.Blue;

                        if (isRed)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                judge.RedPoints += scoreData.Score;
                                UpdateCurrentRoundVotes();
                            });

                            if (competitor1 != null)
                            {
                                await SaveRefereeData(judge, competitor1.Id, scoreData.Score, false, null, scoreData.Round);
                            }
                        }
                        else if (isBlue)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                judge.BluePoints += scoreData.Score;
                                UpdateCurrentRoundVotes();
                            });

                            if (competitor2 != null)
                            {
                                await SaveRefereeData(judge, competitor2.Id, scoreData.Score, false, null, scoreData.Round);
                            }
                        }
                    }
                    else if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
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
                        });

                        var competitor = GetCompetitor(CurrentDraw, true);
                        if (competitor != null)
                        {
                            await SaveRefereeData(judge, competitor.Id, scoreData.Score, false, scoreData.Code, scoreData.Round);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing judge score from client {ClientId}", args.ClientId);
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
                        judge = judgesSnapshot.FirstOrDefault(j => j.Group == clientState.Group && j.Number == clientState.Number);
                    }
                    else
                    {
                        judge = judgesSnapshot.FirstOrDefault(j => j.Number == clientState.Number)
                             ?? judgesSnapshot.FirstOrDefault(j => j.Name == clientState.Name);
                    }

                    if (judge != null)
                    {
                        _clientJudgeMapping[args.ClientId] = judge;

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            judge.IsConnected = true;
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
                            }
                            else
                            {
                                UpdateCurrentRoundVotes();
                            }
                        });

                        if (CurrentDraw != null && CurrentDraw.Type == RoundType.Order)
                        {
                            var competitor = GetCompetitor(CurrentDraw, true);
                            if (competitor != null && clientState.Codes != null)
                            {
                                await _dbSemaphore.WaitAsync();
                                IReadOnlyList<RefereeDataModel>? existingData;
                                try
                                {
                                    existingData = await _refereeDataRepository.GetAllAsync();
                                }
                                finally
                                {
                                    _dbSemaphore.Release();
                                }

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
            // Le serveur est actif : ne pas l'arrêter lors d'un OnDisappearing
            // (qui est déclenché par l'affichage d'un Popup CommunityToolkit comme TimerPickerPopup
            // ou la perte temporaire de focus de la fenêtre).
            // L'arrêt contrôlé est géré explicitement par StopServerCommand / RequestBackNavigationCommand.
            await base.OnDisappearing();
            return;
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
            string targetClient = clientId.Contains('#') ? clientId.Split('#')[0] : clientId;
            await _bluetoothServer.SendToClientAsync($"{Constants.Message.JUDGE_STATE}{stateJson}", targetClient);
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
        if (competitionType == CompetitionType.Taolu)
        {
            bool isAlreadyTaolu = Judges.Count == 9
                && Judges.Count(j => j.Group == "A") == 3
                && Judges.Count(j => j.Group == "B") == 3
                && Judges.Count(j => j.Group == "C") == 3;

            if (isAlreadyTaolu && JudgeGroups.Count == 3)
            {
                // Conserver les instances de juges et groupes existants pour préserver l'état IsConnected et les mappings
                OnPropertyChanged(nameof(Judges));
                OnPropertyChanged(nameof(JudgeGroups));
                return;
            }

            Judges.Clear();
            JudgeGroups.Clear();

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
            JudgeGroups.Clear();

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

        lock (_judgesLock)
        {
            _judgesSnapshot = Judges.ToList();
        }

        OnPropertyChanged(nameof(Judges));
        OnPropertyChanged(nameof(JudgeGroups));
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

        if (CompetitionId == 0 && _currentCompetition != null)
        {
            CompetitionId = _currentCompetition.Id;
        }

        if (_currentCompetition != null)
        {
            try
            {
                _lastSentCompToClient[clientId] = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(ServerName))
                {
                    if (!_lastSentServerNameToClient.TryGetValue(clientId, out var lastSent) || lastSent != ServerName)
                    {
                        _lastSentServerNameToClient[clientId] = ServerName;
                        await _bluetoothServer.SendToClientAsync($"{Constants.Message.SERVER_NAME}{ServerName}", clientId);
                    }
                }

                var lightComp = GetCompetitionWithoutMatches(_currentCompetition);
                var json = JsonSerializer.Serialize(lightComp, _jsonOptions);

                await _bluetoothServer.SendToClientAsync($"{Constants.Message.COMPETITION_DATA}{json}", clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending competition data to client {ClientId}", clientId);
            }
        }
    }

    private CompetitionModel GetCompetitionWithoutMatches(CompetitionModel competition)
    {
        var comp = new CompetitionModel
        {
            Id = competition.Id,
            Name = competition.Name,
            Date = competition.Date,
            Type = competition.Type,
            Categories = new List<CategoryModel>()
        };

        if (competition.Categories != null)
        {
            foreach (var cat in competition.Categories)
            {
                var newCat = new CategoryModel
                {
                    Id = cat.Id,
                    Type = cat.Type,
                    Genre = cat.Genre,
                    WeightMin = cat.WeightMin,
                    WeightMax = cat.WeightMax,
                    AgeRangeId = cat.AgeRangeId,
                    AgeRange = cat.AgeRange,
                    RoundType = cat.RoundType,
                    HasBronzeMatch = cat.HasBronzeMatch,
                    CompetitionId = cat.CompetitionId,
                    Competitors = cat.Competitors != null
                        ? cat.Competitors.Select(cc => new CompetitorCategoryModel
                        {
                            Id = cc.Id,
                            CompetitorId = cc.CompetitorId,
                            CategoryId = cc.CategoryId,
                            Category = null,
                            Competitor = cc.Competitor != null ? new CompetitorModel
                            {
                                Id = cc.Competitor.Id,
                                FirstName = cc.Competitor.FirstName,
                                LastName = cc.Competitor.LastName,
                                Club = cc.Competitor.Club,
                                BirthDate = cc.Competitor.BirthDate,
                                Genre = cc.Competitor.Genre,
                                Weight = cc.Competitor.Weight,
                                CountryIsoCode = cc.Competitor.CountryIsoCode,
                                Photo = cc.Competitor.Photo
                            } : null,
                            Warnings = cc.Warnings
                        }).ToList()
                        : new List<CompetitorCategoryModel>()
                };

                if (cat.Draw != null)
                {
                    newCat.Draw = new DrawModel
                    {
                        Id = cat.Draw.Id,
                        CategoryId = cat.Id,
                        DrawKnockouts = new List<DrawKnockoutModel>(),
                        DrawPools = new List<DrawPoolsModel>(),
                        DrawOrders = new List<DrawOrderModel>()
                    };
                }

                comp.Categories.Add(newCat);
            }
        }

        return comp;
    }

    private MatchInfoData CreateMatchInfoData(IDrawModel draw)
    {
        var matchData = new MatchInfoData
        {
            Id = draw.Id,
            Type = draw.Type,
            Round = draw.CurrentRound,
            CategoryId = draw.Draw?.CategoryId,
            CategoryName = draw.Draw?.Category?.Name
        };

        if (draw is DrawKnockoutModel k)
        {
            matchData.DrawKnockout = k;
            matchData.Competitor1 = k.Competitor1;
            matchData.Competitor2 = k.Competitor2;
            matchData.StageName = k.StageName;

            if (matchData.Competitor1 == null && k.Competitor1Id.HasValue)
            {
                matchData.Competitor1 = _currentCompetition?.Categories
                    .SelectMany(c => c.Competitors ?? Enumerable.Empty<CompetitorCategoryModel>())
                    .Select(cc => cc.Competitor)
                    .FirstOrDefault(c => c?.Id == k.Competitor1Id.Value);
            }

            if (matchData.Competitor2 == null && k.Competitor2Id.HasValue)
            {
                matchData.Competitor2 = _currentCompetition?.Categories
                    .SelectMany(c => c.Competitors ?? Enumerable.Empty<CompetitorCategoryModel>())
                    .Select(cc => cc.Competitor)
                    .FirstOrDefault(c => c?.Id == k.Competitor2Id.Value);
            }
        }
        else if (draw is DrawPoolsModel p)
        {
            matchData.DrawPool = p;
            matchData.Competitor1 = p.Competitor1;
            matchData.Competitor2 = p.Competitor2;
            matchData.StageName = $"Poule - Match #{p.Order}";

            if (matchData.Competitor1 == null && p.Competitor1Id.HasValue)
            {
                matchData.Competitor1 = _currentCompetition?.Categories
                    .SelectMany(c => c.Competitors ?? Enumerable.Empty<CompetitorCategoryModel>())
                    .Select(cc => cc.Competitor)
                    .FirstOrDefault(c => c?.Id == p.Competitor1Id.Value);
            }

            if (matchData.Competitor2 == null && p.Competitor2Id.HasValue)
            {
                matchData.Competitor2 = _currentCompetition?.Categories
                    .SelectMany(c => c.Competitors ?? Enumerable.Empty<CompetitorCategoryModel>())
                    .Select(cc => cc.Competitor)
                    .FirstOrDefault(c => c?.Id == p.Competitor2Id.Value);
            }
        }
        else if (draw is DrawOrderModel o)
        {
            matchData.DrawOrder = o;
            matchData.Competitor1 = o.Competitor;

            if (matchData.Competitor1 == null && o.CompetitorId > 0)
            {
                matchData.Competitor1 = _currentCompetition?.Categories
                    .SelectMany(c => c.Competitors ?? Enumerable.Empty<CompetitorCategoryModel>())
                    .Select(cc => cc.Competitor)
                    .FirstOrDefault(c => c?.Id == o.CompetitorId);
            }
        }

        return matchData;
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
    internal async Task Init()
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

        if (CompetitionId == 0 && _currentCompetition != null)
        {
            CompetitionId = _currentCompetition.Id;
        }

        SetupJudges(_currentCompetition?.Type ?? CompetitionType.Sanda);

        var knockouts = (await _drawKnockoutService.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();
        var orders = (await _drawOrderService.GetAllAsync("Draw.Category.AgeRange", "Competitor.Country"))?.ToList();
        var pools = (await _drawPoolsModel.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();

        // Lier les DrawKnockouts à leur Draw pour permettre le calcul dynamique correct de StageName
        if (knockouts != null && knockouts.Count > 0)
        {
            var knockoutsByDraw = knockouts.GroupBy(k => k.DrawId);
            foreach (var group in knockoutsByDraw)
            {
                var list = group.OrderBy(k => k.Order).ToList();
                foreach (var k in list)
                {
                    if (k.Draw != null)
                    {
                        k.Draw.DrawKnockouts = list;
                    }
                }
            }
        }

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

        // Ordonnancement : Préliminaires (0) -> Petites finales (1) -> Finales (2)
        var sortedDraws = allDraws
            .OrderBy(d => GetDrawPhasePriority(d))
            .ThenBy(d => d.GlobalOrder > 0 ? d.GlobalOrder : int.MaxValue)
            .ThenBy(d => d.Draw?.CategoryId ?? 0)
            .ThenBy(d => GetDrawLocalOrder(d))
            .ToList();

        for (int i = 0; i < sortedDraws.Count; i++)
        {
            sortedDraws[i].GlobalOrder = i + 1;
        }

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
        OnPropertyChanged(nameof(Draws));

        await BroadcastMatchInfo();

        UpdateCurrentRoundVotes();
    }

    /// <summary>
    /// Calcule la priorité de phase pour un tirage : Préliminaires / Quarts / Demis (0) -> Petites finales (1) -> Finales (2)
    /// </summary>
    private static int GetDrawPhasePriority(IDrawModel draw)
    {
        if (draw is DrawKnockoutModel ko)
        {
            if (ko.IsBronzeMatch || string.Equals(ko.StageName, "Petite finale", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(ko.StageName, "Finale", StringComparison.OrdinalIgnoreCase)) return 2;
            return 0;
        }
        return 0;
    }

    /// <summary>
    /// Retourne l'ordre local du combat dans sa catégorie
    /// </summary>
    private static int GetDrawLocalOrder(IDrawModel draw)
    {
        return draw switch
        {
            DrawKnockoutModel ko => ko.Order,
            DrawPoolsModel pool => pool.Order,
            DrawOrderModel order => order.Order,
            _ => 0
        };
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
    private async Task SaveRefereeData(JudgeModel judge, int? competitorId, double score, bool isCorrection = false, MvtTimeCodeModel? code = null, int? round = null)
    {
        if (CurrentDraw == null)
        {
            return;
        }

        if (code != null)
        {
            code.Score = score;
        }

        var elapsed = _totalRoundTime > _timeLeft ? (_totalRoundTime - _timeLeft) : TimeSpan.Zero;

        int roundNumber = (round.HasValue && round.Value > 0)
            ? round.Value
            : (CurrentDraw.CurrentRound > 0 ? CurrentDraw.CurrentRound : 1);

        var data = new RefereeDataModel
        {
            Date = DateTime.Now,
            MatchTimer = elapsed,
            Referee = judge.Name,
            Score = score,
            Data = (code != null) ? JsonSerializer.Serialize(code) : String.Empty,
            IsCorrection = isCorrection,
            CompetitorId = competitorId,
            Round = roundNumber
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

        await Task.Run(async () =>
        {
            await _dbSemaphore.WaitAsync();
            try
            {
                await _refereeDataRepository.AddAsync(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving referee data asynchronously");
            }
            finally
            {
                _dbSemaphore.Release();
            }
        });
    }

    /// <summary>
    /// 
    /// </summary>
    private async Task StopTimer()
    {
        _timer?.Stop();
        IsTimerRunning = false;
        string timerMsg = $"{Constants.Message.TIMER_SET}{TimeLeftDisplay}";

        _ = Task.Run(async () =>
        {
            await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_PAUSE);
            await _bluetoothServer.SendToAllAsync(timerMsg);
        });

        await Task.CompletedTask;
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
                var matchData = CreateMatchInfoData(CurrentDraw);
                var json = JsonSerializer.Serialize(matchData, _jsonOptions);
                // Fire-and-forget pour ne pas bloquer le thread UI sur _sendSemaphore
                _ = Task.Run(async () =>
                {
                    await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_INFO}{json}");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting match info");
            }
        }
        else
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_INFO}{{}}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting empty match info");
                }
            });
        }

        await Task.CompletedTask;
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
            string timerMsg = $"{Constants.Message.TIMER_SET}{TimeLeftDisplay}";

            _ = Task.Run(async () =>
            {
                await _bluetoothServer.SendToAllAsync(timerMsg);
                await Task.Delay(50);
                await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_START);
            });
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Commande permettant de mettre en pause le timer
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task PauseTimer()
    {
        await StopTimer();
    }

    /// <summary>
    /// Commande permettant de réinitialiser le timer
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ResetTimer()
    {
        await StopTimer();

        _totalRoundTime = TimeSpan.FromMinutes(2);
        _timeLeft = TimeSpan.FromMinutes(2); // Default reset

        OnPropertyChanged(nameof(TimeLeftDisplay));
        string timerMsg = $"{Constants.Message.TIMER_SET}{TimeLeftDisplay}";

        _ = Task.Run(async () =>
        {
            await _bluetoothServer.SendToAllAsync(timerMsg);
        });
    }

    /// <summary>
    /// Commande permettant de définir un temps pour le timer
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task SetTimer()
    {
        // Utilise un Popup dédié plutôt que DisplayActionSheetAsync
        // pour éviter le bug MAUI où le dialog Shell ne s'ouvre pas
        // la 2ème fois quand des handlers Bluetooth async sont actifs
        // sur le thread principal (MainThread.BeginInvokeOnMainThread).
        var popupResult = await _popupService.ShowPopupAsync<TimerPickerPopupViewModel, string>(
            Shell.Current,
            options: new PopupOptions() { Shadow = null }
        );

        var result = popupResult.Result;

        if (!string.IsNullOrEmpty(result))
        {
            await StopTimer();

            if (TimeSpan.TryParseExact(result, @"m\:ss", null, out var newTime))
            {
                _totalRoundTime = newTime;
                _timeLeft = newTime;

                OnPropertyChanged(nameof(TimeLeftDisplay));
                string timerMsg = $"{Constants.Message.TIMER_SET}{TimeLeftDisplay}";

                _ = Task.Run(async () =>
                {
                    await _bluetoothServer.SendToAllAsync(timerMsg);
                });
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
            await Task.Delay(250);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying clients on StopServer");
        }

        // Marquer tous les juges comme déconnectés localement
        foreach (var judge in Judges)
        {
            judge.IsConnected = false;
        }
        _clientJudgeMapping.Clear();

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
        var activeJudges = Judges.Where(j => j.IsConnected || j.HasConnected).ToList();
        if (activeJudges.Count == 0 && !Judges.Any(j => j.IsConnected || j.HasConnected))
        {
            activeJudges = Judges.ToList();
        }

        int redVotes = (int)activeJudges.Sum(j => j.RedPoints);
        int blueVotes = (int)activeJudges.Sum(j => j.BluePoints);
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
                        var koRoundUpdate = new DrawKnockoutModel
                        {
                            Id = k1.Id,
                            DrawId = k1.DrawId > 0 ? k1.DrawId : (k1.Draw?.Id ?? 0),
                            Order = k1.Order,
                            IsBronzeMatch = k1.IsBronzeMatch,
                            CurrentRound = k1.CurrentRound,
                            RoundResultsJson = k1.RoundResultsJson,
                            Competitor1Id = k1.Competitor1Id,
                            Competitor2Id = k1.Competitor2Id,
                            WinnerId = k1.WinnerId,
                            LooserId = k1.LooserId,
                            IsFinished = k1.IsFinished,
                            GlobalOrder = k1.GlobalOrder
                        };
                        await _drawKnockoutService.UpdateAsync(koRoundUpdate);
                    }
                    else if (CurrentDraw is DrawPoolsModel p1)
                    {
                        var poolRoundUpdate = new DrawPoolsModel
                        {
                            Id = p1.Id,
                            DrawId = p1.DrawId > 0 ? p1.DrawId : (p1.Draw?.Id ?? 0),
                            Order = p1.Order,
                            CurrentRound = p1.CurrentRound,
                            RoundResultsJson = p1.RoundResultsJson,
                            Competitor1Id = p1.Competitor1Id,
                            Competitor2Id = p1.Competitor2Id,
                            WinnerId = p1.WinnerId,
                            LooserId = p1.LooserId,
                            IsFinished = p1.IsFinished,
                            GlobalOrder = p1.GlobalOrder
                        };
                        await _drawPoolsModel.UpdateAsync(poolRoundUpdate);
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
        bool _useTimer = false;

        if (CurrentDraw is DrawKnockoutModel knockout)
        {
            _useTimer = true;
            // IMPORTANT : Récupérer targetDrawId et les concurrents AVANT de détacher quoi que ce soit
            int targetDrawId = knockout.DrawId > 0 
                ? knockout.DrawId 
                : (knockout.Draw?.Id > 0 ? knockout.Draw.Id : (CurrentDraw?.Draw?.Id ?? 0));

            winner = color == "Red" ? knockout.Competitor1 : knockout.Competitor2;
            looser = color == "Red" ? knockout.Competitor2 : knockout.Competitor1;

            if (winner != null)
            {
                knockout.WinnerId = winner.Id;
                knockout.LooserId = looser?.Id;
                knockout.IsFinished = true;
                if (targetDrawId > 0 && knockout.DrawId == 0)
                {
                    knockout.DrawId = targetDrawId;
                }

                // Utiliser une entité détachée sans propriétés de navigation pour la mise à jour EF Core
                // afin d'éviter d'effacer les concurrents à l'écran pendant la transition de match
                var updateKo = new DrawKnockoutModel
                {
                    Id = knockout.Id,
                    DrawId = targetDrawId > 0 ? targetDrawId : knockout.DrawId,
                    Order = knockout.Order,
                    IsBronzeMatch = knockout.IsBronzeMatch,
                    CurrentRound = knockout.CurrentRound,
                    RoundResultsJson = knockout.RoundResultsJson,
                    Competitor1Id = knockout.Competitor1Id,
                    Competitor2Id = knockout.Competitor2Id,
                    WinnerId = winner.Id,
                    LooserId = looser?.Id,
                    IsFinished = true,
                    GlobalOrder = knockout.GlobalOrder
                };
                await _drawKnockoutService.UpdateAsync(updateKo);

                // Propagation logic for Knockouts
                if (targetDrawId == 0)
                {
                    var existingK = (await _drawKnockoutService.GetAllAsync("Draw"))?.FirstOrDefault(k => k.Id == knockout.Id);
                    if (existingK != null)
                    {
                        targetDrawId = existingK.DrawId > 0 ? existingK.DrawId : (existingK.Draw?.Id ?? 0);
                    }
                }

                var allKnockouts = (await _drawKnockoutService.GetAllAsync("Draw", "Competitor1", "Competitor2"))?
                    .Where(k => targetDrawId > 0 ? (k.DrawId == targetDrawId || (k.Draw != null && k.Draw.Id == targetDrawId)) : true)
                    .OrderBy(k => k.Order)
                    .ToList();
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
                                DrawKnockoutModel? nextMatch;
                                if (matchesInRound == 2)
                                {
                                    // Le vainqueur de demi-finale va en Finale (qui a lieu APRÈS la petite finale)
                                    nextMatch = allKnockouts.LastOrDefault(k => !k.IsBronzeMatch);
                                }
                                else
                                {
                                    int nextMatchOrder = nextRoundStart + (localIndex + 1) / 2;
                                    nextMatch = allKnockouts.FirstOrDefault(k => k.Order == nextMatchOrder);
                                }

                                if (nextMatch != null)
                                {
                                    int targetNextDrawId = (targetDrawId > 0 && nextMatch.DrawId == 0) ? targetDrawId : nextMatch.DrawId;
                                    int? nextComp1Id = (localIndex % 2 != 0) ? winner.Id : nextMatch.Competitor1Id;
                                    int? nextComp2Id = (localIndex % 2 == 0) ? winner.Id : nextMatch.Competitor2Id;

                                    var updateNextMatch = new DrawKnockoutModel
                                    {
                                        Id = nextMatch.Id,
                                        DrawId = targetNextDrawId,
                                        Order = nextMatch.Order,
                                        IsBronzeMatch = nextMatch.IsBronzeMatch,
                                        CurrentRound = nextMatch.CurrentRound,
                                        RoundResultsJson = nextMatch.RoundResultsJson,
                                        Competitor1Id = nextComp1Id,
                                        Competitor2Id = nextComp2Id,
                                        WinnerId = nextMatch.WinnerId,
                                        LooserId = nextMatch.LooserId,
                                        IsFinished = nextMatch.IsFinished,
                                        GlobalOrder = nextMatch.GlobalOrder
                                    };
                                    await _drawKnockoutService.UpdateAsync(updateNextMatch);
                                }

                                if (matchesInRound == 2 && looser != null)
                                {
                                    // Le perdant de demi-finale va en Petite finale (qui a lieu AVANT la finale)
                                    var bronzeMatch = allKnockouts.FirstOrDefault(k => k.IsBronzeMatch);
                                    if (bronzeMatch != null)
                                    {
                                        int targetBronzeDrawId = (targetDrawId > 0 && bronzeMatch.DrawId == 0) ? targetDrawId : bronzeMatch.DrawId;
                                        int? bronzeComp1Id = (localIndex % 2 != 0) ? looser.Id : bronzeMatch.Competitor1Id;
                                        int? bronzeComp2Id = (localIndex % 2 == 0) ? looser.Id : bronzeMatch.Competitor2Id;

                                        var updateBronze = new DrawKnockoutModel
                                        {
                                            Id = bronzeMatch.Id,
                                            DrawId = targetBronzeDrawId,
                                            Order = bronzeMatch.Order,
                                            IsBronzeMatch = bronzeMatch.IsBronzeMatch,
                                            CurrentRound = bronzeMatch.CurrentRound,
                                            RoundResultsJson = bronzeMatch.RoundResultsJson,
                                            Competitor1Id = bronzeComp1Id,
                                            Competitor2Id = bronzeComp2Id,
                                            WinnerId = bronzeMatch.WinnerId,
                                            LooserId = bronzeMatch.LooserId,
                                            IsFinished = bronzeMatch.IsFinished,
                                            GlobalOrder = bronzeMatch.GlobalOrder
                                        };
                                        await _drawKnockoutService.UpdateAsync(updateBronze);
                                    }
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
            _useTimer = true;

            winner = color == "Red" ? pool.Competitor1 : pool.Competitor2;
            looser = color == "Red" ? pool.Competitor2 : pool.Competitor1;

            if (winner != null)
            {
                pool.WinnerId = winner.Id;
                pool.LooserId = looser?.Id;
                pool.IsFinished = true;

                var updatePool = new DrawPoolsModel
                {
                    Id = pool.Id,
                    DrawId = pool.DrawId,
                    Order = pool.Order,
                    CurrentRound = pool.CurrentRound,
                    RoundResultsJson = pool.RoundResultsJson,
                    Competitor1Id = pool.Competitor1Id,
                    Competitor2Id = pool.Competitor2Id,
                    WinnerId = winner.Id,
                    LooserId = looser?.Id,
                    IsFinished = true,
                    GlobalOrder = pool.GlobalOrder
                };
                await _drawPoolsModel.UpdateAsync(updatePool);
            }
        }
        else if (CurrentDraw is DrawOrderModel order)
        {
            winner = order.Competitor;
            order.IsFinished = true;
            if (JudgeGroups.Count > 0)
            {
                double totalScore = Math.Round(JudgeGroups.Sum(g => g.GlobalScore), 2);
                order.RoundResultsJson = totalScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            }

            var updateOrder = new DrawOrderModel
            {
                Id = order.Id,
                DrawId = order.DrawId,
                Order = order.Order,
                CompetitorId = order.CompetitorId,
                RoundResultsJson = order.RoundResultsJson,
                IsFinished = true,
                GlobalOrder = order.GlobalOrder
            };
            await _drawOrderService.UpdateAsync(updateOrder);
        }

        if (winner != null)
        {
            ResetJudgeScores();

            if (_useTimer)
            {
                await ResetTimer();
            }

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

    /// <summary>
    /// Commande permettant de déclarer un ex-æquo pour le round actuel et de passer au round suivant
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task SetExAequo()
    {
        if (CurrentDraw == null)
        {
            return;
        }

        var activeJudges = Judges.Where(j => j.IsConnected || j.HasConnected).ToList();
        if (activeJudges.Count == 0 && !Judges.Any(j => j.IsConnected || j.HasConnected))
            activeJudges = Judges.ToList();

        int redVotes = (int)activeJudges.Sum(j => j.RedPoints);
        int blueVotes = (int)activeJudges.Sum(j => j.BluePoints);

        RoundResults.Add(new RoundResultModel
        {
            RoundNumber = CurrentDraw.CurrentRound,
            RedVotes = redVotes,
            BlueVotes = blueVotes,
            WinnerColor = "Draw"
        });

        CurrentDraw.RoundResultsJson = JsonSerializer.Serialize(RoundResults, _jsonOptions);
        CurrentDraw.CurrentRound++;

        if (CurrentDraw is DrawKnockoutModel k1)
        {
            await _drawKnockoutService.UpdateAsync(k1);
        }
        else if (CurrentDraw is DrawPoolsModel p1)
        {
            await _drawPoolsModel.UpdateAsync(p1);
        }
        else if (CurrentDraw is DrawOrderModel o1)
        {
            await _drawOrderService.UpdateAsync(o1);
        }

        ResetJudgeScores();
        await ResetTimer();
        await BroadcastMatchInfo();
    }

    /// <summary>
    /// Commande permettant de passer au round suivant
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task NextRound()
    {
        await SetExAequo();
    }

    /// <summary>
    /// Commande permettant de passer au match suivant
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task NextMatch()
    {
        if (CurrentDraw != null)
        {
            if (CurrentDraw is DrawKnockoutModel k && k.WinnerId == null)
            {
                // Si l'utilisateur clique sur Next Match sans avoir déclaré de vainqueur via SetWinner,
                // déterminer le vainqueur selon les votes/rounds ou les points des juges (par défaut Rouge si égalité/aucun vote)
                int redWins = RoundResults.Count(r => r.WinnerColor == "Red");
                int blueWins = RoundResults.Count(r => r.WinnerColor == "Blue");
                string winningColor = (blueWins > redWins) ? "Blue" : "Red";
                if (redWins == 0 && blueWins == 0)
                {
                    int redVotes = (int)Judges.Sum(j => j.RedPoints);
                    int blueVotes = (int)Judges.Sum(j => j.BluePoints);
                    winningColor = (blueVotes > redVotes) ? "Blue" : "Red";
                }

                while (RoundResults.Count(r => r.WinnerColor == winningColor) < 2)
                {
                    RoundResults.Add(new RoundResultModel
                    {
                        RoundNumber = CurrentDraw.CurrentRound,
                        WinnerColor = winningColor
                    });
                }
                CurrentDraw.RoundResultsJson = JsonSerializer.Serialize(RoundResults, _jsonOptions);
                await SetWinner(winningColor);
                return;
            }

            CurrentDraw.IsFinished = true;
            if (CurrentDraw is DrawKnockoutModel kFinished)
            {
                await _drawKnockoutService.UpdateAsync(kFinished);
            }
            else if (CurrentDraw is DrawPoolsModel p)
            {
                await _drawPoolsModel.UpdateAsync(p);
            }
            else if (CurrentDraw is DrawOrderModel o)
            {
                if (JudgeGroups.Count > 0 && string.IsNullOrEmpty(o.RoundResultsJson))
                {
                    double totalScore = Math.Round(JudgeGroups.Sum(g => g.GlobalScore), 2);
                    o.RoundResultsJson = totalScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                }
                await _drawOrderService.UpdateAsync(o);
            }
        }

        ResetJudgeScores();
        await ResetTimer();
        await Init();
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
    /// Demande la navigation retour. Si le serveur est actif, affiche une popup d'avertissement.
    /// Si confirmé, arrête le serveur et effectue le retour arrière.
    /// </summary>
    /// <returns>True si le retour arrière a été effectué, False si annulé.</returns>
    [RelayCommand]
    public async Task<bool> RequestBackNavigation()
    {
        if (_isNavigatingBack)
        {
            return false;
        }

        try
        {
            _isNavigatingBack = true;

            if (IsServerStarted)
            {
                bool confirm = await _alertService.ShowConfirmation(
                    "Serveur actif",
                    "Le retour en arrière va arrêter le serveur. Voulez-vous continuer ?",
                    "Oui",
                    "Annuler");

                if (!confirm)
                {
                    return false;
                }

                await StopServer();
            }

            await NavigateBackAsync();
            return true;
        }
        finally
        {
            _isNavigatingBack = false;
        }
    }

    private async Task NavigateBackAsync()
    {
        if (IsSideSheetOpen)
        {
            IsSideSheetOpen = false;
        }

        if (Shell.Current != null)
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                var location = Shell.Current.CurrentState?.Location?.OriginalString;
                if (location != null && !location.Contains("MainPage"))
                {
                    await Shell.Current.GoToAsync("///MainPage");
                }
                else
                {
                    await Shell.Current.GoToAsync("..");
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task GoToDraws()
    {
        IsSideSheetOpen = false;

        if (IsServerStarted)
        {
            bool confirm = await _alertService.ShowConfirmation(
                "Serveur actif",
                "Le retour en arrière va arrêter le serveur. Voulez-vous continuer ?",
                "Oui",
                "Annuler");

            if (!confirm)
            {
                return;
            }

            await StopServer();
        }

        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync($"DrawsManagementPage?CompetitionId={CompetitionId}");
        }
    }
    #endregion
}