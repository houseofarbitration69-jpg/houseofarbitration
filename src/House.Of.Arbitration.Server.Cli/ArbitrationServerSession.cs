using System.Collections.ObjectModel;
using System.Text.Json;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Server.Cli;

public class MatchItem
{
    public int Id { get; set; }
    public RoundType Type { get; set; } = RoundType.Knockouts;
    public string Category { get; set; } = "Senior -70kg";
    public string RedCompetitor { get; set; } = "Combattant Rouge";
    public string BlueCompetitor { get; set; } = "Combattant Bleu";
    public int RedScore { get; set; } = 0;
    public int BlueScore { get; set; } = 0;
    public bool IsFinished { get; set; }
}

/// <summary>
/// Gestionnaire central de la session d'arbitrage côté serveur.
/// Gère les juges, les matchs, le chronomètre et le protocole de communication.
/// </summary>
public class ArbitrationServerSession : IDisposable
{
    private readonly WindowsBluetoothServer _bluetoothServer;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly Dictionary<string, JudgeModel> _clientJudgeMapping = new();
    private readonly List<MatchItem> _matches = new();

    private System.Threading.Timer? _timer;
    private TimeSpan _timeLeft = TimeSpan.FromMinutes(2);
    private int _currentMatchIndex = 0;

    public WindowsBluetoothServer BluetoothServer => _bluetoothServer;

    public CompetitionType CurrentCompetitionType { get; private set; } = CompetitionType.Sanda;
    public ObservableCollection<JudgeModel> Judges { get; } = new();
    public ObservableCollection<JudgeGroupModel> JudgeGroups { get; } = new();

    public MatchItem? CurrentMatch => _matches.Count > 0 && _currentMatchIndex < _matches.Count ? _matches[_currentMatchIndex] : null;
    public IReadOnlyList<MatchItem> Matches => _matches;

    public bool IsTimerRunning { get; private set; }
    public string TimeLeftDisplay => $"{(int)_timeLeft.TotalMinutes:D2}:{_timeLeft.Seconds:D2}";

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<JudgeModel>? JudgeConnected;
    public event EventHandler<JudgeModel>? JudgeDisconnected;
    public event EventHandler<(JudgeModel Judge, string Details)>? ScoreUpdated;
    public event EventHandler<string>? TimerTick;

    public ArbitrationServerSession(WindowsBluetoothServer bluetoothServer)
    {
        _bluetoothServer = bluetoothServer;
        _bluetoothServer.MessageReceived += OnBluetoothMessageReceived;
        _bluetoothServer.ClientDisconnected += OnClientDisconnected;

        // Initialisation des données par défaut
        SetupDefaultMatches();
        SetupJudges(CompetitionType.Sanda);
    }

    /// <summary>
    /// Configure la session pour Sanda ou Taolu.
    /// </summary>
    public async Task SetCompetitionTypeAsync(CompetitionType type)
    {
        CurrentCompetitionType = type;
        SetupJudges(type);
        StatusChanged?.Invoke(this, $"Mode de compétition défini sur : {type}");

        if (_bluetoothServer.IsAdvertising)
        {
            await BroadcastCompetitionDataAsync();
            await BroadcastMatchInfoAsync();
        }
    }

    private void SetupJudges(CompetitionType type)
    {
        Judges.Clear();
        JudgeGroups.Clear();
        _clientJudgeMapping.Clear();

        if (type == CompetitionType.Taolu)
        {
            var groupA = new JudgeGroupModel("Groupe A", "Qualité", Constants.DEFAULT_SCORE_GROUP_A);
            var groupB = new JudgeGroupModel("Groupe B", "Performance", Constants.DEFAULT_SCORE_GROUP_B);
            var groupC = new JudgeGroupModel("Groupe C", "Difficulté", Constants.DEFAUTL_SCORE_GROUP_C);

            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE A{i}", Number = i, Group = "A", Score = Constants.DEFAULT_SCORE_GROUP_A };
                Judges.Add(judge);
                groupA.Judges.Add(judge);
            }
            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE B{i}", Number = i, Group = "B", Score = Constants.DEFAULT_SCORE_GROUP_B };
                Judges.Add(judge);
                groupB.Judges.Add(judge);
            }
            for (int i = 1; i <= 3; i++)
            {
                var judge = new JudgeModel { Name = $"JUGE C{i}", Number = i, Group = "C", Score = Constants.DEFAUTL_SCORE_GROUP_C };
                Judges.Add(judge);
                groupC.Judges.Add(judge);
            }

            JudgeGroups.Add(groupA);
            JudgeGroups.Add(groupB);
            JudgeGroups.Add(groupC);
        }
        else
        {
            // Sanda : 5 juges
            for (int i = 1; i <= 5; i++)
            {
                Judges.Add(new JudgeModel
                {
                    Name = $"JUGE {i}",
                    Number = i,
                    Group = null,
                    RedPoints = 0,
                    BluePoints = 0
                });
            }
        }
    }

    private void SetupDefaultMatches()
    {
        _matches.Clear();
        _matches.Add(new MatchItem { Id = 1, Type = RoundType.Knockouts, Category = "Sanda -65kg", RedCompetitor = "Dupont (Rouge)", BlueCompetitor = "Martin (Bleu)" });
        _matches.Add(new MatchItem { Id = 2, Type = RoundType.Knockouts, Category = "Sanda -70kg", RedCompetitor = "Zhang (Rouge)", BlueCompetitor = "Leclerc (Bleu)" });
        _matches.Add(new MatchItem { Id = 3, Type = RoundType.Order, Category = "Taolu Changquan", RedCompetitor = "Wang (Athlète)", BlueCompetitor = "" });
        _matches.Add(new MatchItem { Id = 4, Type = RoundType.Order, Category = "Taolu Nanquan", RedCompetitor = "Dubois (Athlète)", BlueCompetitor = "" });
    }

    public async Task NextMatchAsync()
    {
        if (_currentMatchIndex + 1 < _matches.Count)
        {
            _currentMatchIndex++;
            ResetMatchScores();
            StatusChanged?.Invoke(this, $"Match suivant : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
        else
        {
            StatusChanged?.Invoke(this, "Dernier match déjà atteint.");
        }
    }

    public async Task PreviousMatchAsync()
    {
        if (_currentMatchIndex > 0)
        {
            _currentMatchIndex--;
            ResetMatchScores();
            StatusChanged?.Invoke(this, $"Match précédent : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
    }

    public async Task SetMatchIndexAsync(int index)
    {
        if (index >= 0 && index < _matches.Count)
        {
            _currentMatchIndex = index;
            ResetMatchScores();
            StatusChanged?.Invoke(this, $"Match actif : #{CurrentMatch?.Id} - {CurrentMatch?.Category}");
            await BroadcastMatchInfoAsync();
        }
    }

    public void ResetMatchScores()
    {
        foreach (var j in Judges)
        {
            j.RedPoints = 0;
            j.BluePoints = 0;
            j.Codes.Clear();

            if (j.Group == "A") j.Score = Constants.DEFAULT_SCORE_GROUP_A;
            else if (j.Group == "B") j.Score = Constants.DEFAULT_SCORE_GROUP_B;
            else if (j.Group == "C") j.Score = Constants.DEFAUTL_SCORE_GROUP_C;
            else j.Score = 10.0;
        }
        StatusChanged?.Invoke(this, "Scores et points réinitialisés pour le match en cours.");
    }

    #region Timer Methods
    public async Task StartTimerAsync()
    {
        if (IsTimerRunning) return;

        IsTimerRunning = true;
        _timer?.Dispose();
        _timer = new System.Threading.Timer(OnTimerCallback, null, 1000, 1000);

        StatusChanged?.Invoke(this, $"Chronomètre DÉMARRÉ ({TimeLeftDisplay})");
        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_START);
    }

    public async Task PauseTimerAsync()
    {
        if (!IsTimerRunning) return;

        IsTimerRunning = false;
        _timer?.Dispose();
        _timer = null;

        StatusChanged?.Invoke(this, $"Chronomètre EN PAUSE ({TimeLeftDisplay})");
        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_PAUSE);
    }

    public async Task StopTimerAsync()
    {
        IsTimerRunning = false;
        _timer?.Dispose();
        _timer = null;
        _timeLeft = TimeSpan.Zero;

        StatusChanged?.Invoke(this, "Chronomètre ARRÊTÉ.");
        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_STOP);
    }

    public async Task SetTimerDurationAsync(TimeSpan duration)
    {
        _timeLeft = duration;
        StatusChanged?.Invoke(this, $"Durée du chronomètre définie à : {TimeLeftDisplay}");
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
    }

    private async void OnTimerCallback(object? state)
    {
        if (_timeLeft.TotalSeconds > 0)
        {
            _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
            TimerTick?.Invoke(this, TimeLeftDisplay);
        }
        else
        {
            await StopTimerAsync();
        }
    }
    #endregion

    #region Protocol & Broadcasts
    public async Task BroadcastMatchInfoAsync()
    {
        if (CurrentMatch == null) return;

        var data = new MatchInfoData
        {
            Id = CurrentMatch.Id,
            Type = CurrentMatch.Type
        };

        string json = JsonSerializer.Serialize(data, _jsonOptions);
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_INFO}{json}");
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
    }

    public async Task BroadcastCompetitionDataAsync()
    {
        if (!string.IsNullOrWhiteSpace(_bluetoothServer.DeviceName))
        {
            await _bluetoothServer.SendToAllAsync($"{Constants.Message.SERVER_NAME}{_bluetoothServer.DeviceName}");
        }

        var comp = new CompetitionModel
        {
            Id = 1,
            Name = "Championnat National Wushu",
            Type = CurrentCompetitionType,
            Date = DateTime.Today
        };

        string json = JsonSerializer.Serialize(comp, _jsonOptions);
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.COMPETITION_DATA}{json}");
    }

    public async Task SendMatchInfoToClientAsync(string clientId)
    {
        if (CurrentMatch == null) return;

        var data = new MatchInfoData
        {
            Id = CurrentMatch.Id,
            Type = CurrentMatch.Type
        };

        string json = JsonSerializer.Serialize(data, _jsonOptions);
        await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.MATCH_INFO}{json}");
        await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
    }

    public async Task SendCompetitionDataToClientAsync(string clientId)
    {
        if (!string.IsNullOrWhiteSpace(_bluetoothServer.DeviceName))
        {
            await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.SERVER_NAME}{_bluetoothServer.DeviceName}");
        }

        var comp = new CompetitionModel
        {
            Id = 1,
            Name = "Championnat National Wushu",
            Type = CurrentCompetitionType,
            Date = DateTime.Today
        };

        string json = JsonSerializer.Serialize(comp, _jsonOptions);
        await _bluetoothServer.SendToClientAsync(clientId, $"{Constants.Message.COMPETITION_DATA}{json}");
    }
    #endregion

    #region Incoming Bluetooth Message Handler
    private async void OnBluetoothMessageReceived(object? sender, (string ClientId, string Message) args)
    {
        string clientId = args.ClientId;
        string message = args.Message;

        try
        {
            if (message.StartsWith(Constants.Message.JUDGE_POSITION))
            {
                var posStr = message.Substring(Constants.Message.JUDGE_POSITION.Length).Trim();

                // Support session ID pour instances multiples sur même PC (ex: "1#instA")
                string sessionKey = clientId;
                if (posStr.Contains('#'))
                {
                    var segs = posStr.Split('#', 2);
                    posStr = segs[0];
                    sessionKey = $"{clientId}#{segs[1]}";
                }

                JudgeModel? judge = null;

                var parts = posStr.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out int num))
                {
                    judge = Judges.FirstOrDefault(j => string.Equals(j.Group, parts[0], StringComparison.OrdinalIgnoreCase) && j.Number == num);
                }
                else if (int.TryParse(posStr, out int position))
                {
                    judge = Judges.FirstOrDefault(j => string.IsNullOrEmpty(j.Group) && j.Number == position)
                         ?? Judges.FirstOrDefault(j => j.Number == position);
                }
                else
                {
                    judge = Judges.FirstOrDefault(j => string.Equals(j.Name, posStr, StringComparison.OrdinalIgnoreCase));
                }

                if (judge != null)
                {
                    // Dissocie l'ancien juge éventuel de cette session client
                    if (_clientJudgeMapping.TryGetValue(sessionKey, out var oldJudge) && oldJudge != judge)
                    {
                        oldJudge.IsConnected = false;
                    }

                    // Dissocie un autre client qui aurait eu ce même juge
                    var existing = _clientJudgeMapping.FirstOrDefault(x => x.Value == judge);
                    if (existing.Key != null && existing.Key != sessionKey)
                    {
                        _clientJudgeMapping.Remove(existing.Key);
                    }

                    judge.IsConnected = true;
                    _clientJudgeMapping[sessionKey] = judge;

                    StatusChanged?.Invoke(this, $"[JUGE ENREGISTRÉ] {judge.Name} (Position: {posStr}) connecté via [{sessionKey}].");
                    JudgeConnected?.Invoke(this, judge);

                    // Envoi immédiat des infos de match et compétition
                    await SendCompetitionDataToClientAsync(clientId);
                    await SendMatchInfoToClientAsync(clientId);
                }
                else
                {
                    StatusChanged?.Invoke(this, $"[ATTENTION] Position juge '{posStr}' demandée par [{clientId}] mais introuvable dans la liste.");
                }
            }
            else if (message.StartsWith(Constants.Message.JUDGE_SCORE))
            {
                string json = message.Substring(Constants.Message.JUDGE_SCORE.Length);
                var scoreModel = JsonSerializer.Deserialize<TransfertScoreModel>(json, _jsonOptions);

                JudgeModel? judge = null;
                // Recherche directe par position spécifiée
                if (!string.IsNullOrEmpty(scoreModel?.JudgePosition))
                {
                    var parts = scoreModel.JudgePosition.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int num))
                    {
                        judge = Judges.FirstOrDefault(j => string.Equals(j.Group, parts[0], StringComparison.OrdinalIgnoreCase) && j.Number == num);
                    }
                    else if (int.TryParse(scoreModel.JudgePosition, out int posNum))
                    {
                        judge = Judges.FirstOrDefault(j => string.IsNullOrEmpty(j.Group) && j.Number == posNum)
                             ?? Judges.FirstOrDefault(j => j.Number == posNum);
                    }
                    else
                    {
                        judge = Judges.FirstOrDefault(j => string.Equals(j.Name, scoreModel.JudgePosition, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (judge == null)
                {
                    _clientJudgeMapping.TryGetValue(clientId, out judge);
                }

                if (scoreModel != null && judge != null)
                {
                    if (scoreModel.Side == Constants.Side.Red)
                    {
                        judge.RedPoints += scoreModel.Score;
                        ScoreUpdated?.Invoke(this, (judge, $"+{scoreModel.Score:F0} Rouge (Total: {judge.RedPoints:F0})"));
                    }
                    else if (scoreModel.Side == Constants.Side.Blue)
                    {
                        judge.BluePoints += scoreModel.Score;
                        ScoreUpdated?.Invoke(this, (judge, $"+{scoreModel.Score:F0} Bleu (Total: {judge.BluePoints:F0})"));
                    }
                    else
                    {
                        judge.Score = scoreModel.Score;
                        if (scoreModel.Code != null)
                        {
                            judge.Codes.Insert(0, scoreModel.Code);
                            ScoreUpdated?.Invoke(this, (judge, $"Déduction {scoreModel.Code.Code?.Code} (-{scoreModel.Code.Code?.Value:F1}) => Nouveau score: {judge.Score:F2}"));
                        }
                        else
                        {
                            ScoreUpdated?.Invoke(this, (judge, $"Score fixé à {judge.Score:F2}"));
                        }
                    }
                }
            }
            else if (message == Constants.Message.GET_MATCH)
            {
                await SendMatchInfoToClientAsync(clientId);
            }
            else if (message == Constants.Message.GET_COMPETITION)
            {
                await SendCompetitionDataToClientAsync(clientId);
            }
            else if (message == Constants.Message.PING)
            {
                await _bluetoothServer.SendToClientAsync(clientId, Constants.Message.PONG);
            }
            else if (message.StartsWith(Constants.Message.JUDGE_DISCONNECT))
            {
                var payload = message.Substring(Constants.Message.JUDGE_DISCONNECT.Length).TrimStart(':');
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
                        string targetKey = $"{clientId}#{instPart}";
                        if (_clientJudgeMapping.TryGetValue(targetKey, out var j))
                        {
                            j.IsConnected = false;
                            keysToRemove.Add(targetKey);
                            JudgeDisconnected?.Invoke(this, j);
                            StatusChanged?.Invoke(this, $"[DÉCONNEXION] Juge {j.Name} s'est déconnecté.");
                        }
                    }

                    if (keysToRemove.Count == 0 && !string.IsNullOrEmpty(posPart))
                    {
                        foreach (var kvp in _clientJudgeMapping.Where(k => k.Key == clientId || k.Key.StartsWith(clientId + "#")))
                        {
                            var j = kvp.Value;
                            if (j.Name == posPart || j.Number.ToString() == posPart || $"{j.Group}_{j.Number}" == posPart)
                            {
                                j.IsConnected = false;
                                keysToRemove.Add(kvp.Key);
                                JudgeDisconnected?.Invoke(this, j);
                                StatusChanged?.Invoke(this, $"[DÉCONNEXION] Juge {j.Name} s'est déconnecté.");
                            }
                        }
                    }
                }

                if (keysToRemove.Count == 0)
                {
                    foreach (var kvp in _clientJudgeMapping.Where(k => k.Key == clientId || k.Key.StartsWith(clientId + "#")))
                    {
                        kvp.Value.IsConnected = false;
                        keysToRemove.Add(kvp.Key);
                        JudgeDisconnected?.Invoke(this, kvp.Value);
                        StatusChanged?.Invoke(this, $"[DÉCONNEXION] Juge {kvp.Value.Name} s'est déconnecté.");
                    }
                }

                foreach (var k in keysToRemove)
                {
                    _clientJudgeMapping.Remove(k);
                }
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erreur traitement message de [{clientId}] : {ex.Message}");
        }
    }

    private void OnClientDisconnected(object? sender, string clientId)
    {
        var matchingKeys = _clientJudgeMapping
            .Where(kvp => kvp.Key == clientId || kvp.Key.StartsWith(clientId + "#"))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in matchingKeys)
        {
            if (_clientJudgeMapping.TryGetValue(key, out var judge))
            {
                judge.IsConnected = false;
                _clientJudgeMapping.Remove(key);
                JudgeDisconnected?.Invoke(this, judge);
                StatusChanged?.Invoke(this, $"Liaison BLE rompue avec {judge.Name} [{key}].");
            }
        }
    }
    #endregion

    /// <summary>
    /// Réinitialise l'état de connexion de tous les juges lors de l'arrêt du serveur.
    /// </summary>
    public void ResetJudgesConnection()
    {
        foreach (var judge in Judges)
        {
            judge.IsConnected = false;
        }
        _clientJudgeMapping.Clear();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _bluetoothServer.Dispose();
        GC.SuppressFinalize(this);
    }
}
