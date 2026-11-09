using System.Diagnostics;
using System.Text.Json;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Judge.Cli;

public class MatchDisplayInfo
{
    public int Id { get; set; }
    public RoundType Type { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Competitor1Name { get; set; } = string.Empty;
    public string Competitor2Name { get; set; } = string.Empty;
    public int? Competitor1Id { get; set; }
    public int? Competitor2Id { get; set; }
}

/// <summary>
/// Session de jugement gérant le protocole, l'état du match, la notation et les commandes vers le serveur.
/// </summary>
public class JudgeSession : IDisposable
{
    private readonly WindowsBluetoothClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private Stopwatch? _pingStopwatch;
    private CancellationTokenSource? _watchdogCts;

    public WindowsBluetoothClient Client => _client;

    public string InstanceId { get; } = Guid.NewGuid().ToString("N").Substring(0, 6);

    public string JudgeGroup { get; private set; } = string.Empty;
    public int JudgePosition { get; private set; } = 1;
    public string PositionCode => !string.IsNullOrEmpty(JudgeGroup) ? $"{JudgeGroup}_{JudgePosition}" : JudgePosition.ToString();

    public double CurrentScore { get; set; } = 10.0;
    public double RedPoints { get; set; } = 0.0;
    public double BluePoints { get; set; } = 0.0;

    public MatchDisplayInfo? CurrentMatch { get; private set; }
    public CompetitionModel? CurrentCompetition { get; private set; }
    public string TimerDisplay { get; private set; } = "02:00";
    public bool IsTimerRunning { get; private set; }

    public List<MvtTimeCodeModel> DeductionsHistory { get; } = new();

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<MatchDisplayInfo>? MatchUpdated;
    public event EventHandler<string>? TimerUpdated;
    public event EventHandler<TimeSpan>? PongReceived;
    public event EventHandler<string>? NotificationReceived;
    public event EventHandler? ServerStopped;

    public bool IsWatchdogActive => _watchdogCts != null && !_watchdogCts.IsCancellationRequested;

    public JudgeSession(WindowsBluetoothClient client)
    {
        _client = client;
        _client.MessageReceived += OnBluetoothMessageReceived;
        _client.Connected += (s, e) => StatusChanged?.Invoke(this, $"Connecté au serveur [{e}].");
        _client.Disconnected += (s, e) =>
        {
            StopWatchdog();
            StatusChanged?.Invoke(this, $"Déconnecté du serveur [{e}].");
        };
    }

    /// <summary>
    /// Envoie la position actuelle du juge au serveur (ex: "JUDGE_POSITION:1" ou "JUDGE_POSITION:A_1").
    /// </summary>
    public async Task<bool> SendCurrentPositionAsync()
    {
        string command = $"{Constants.Message.JUDGE_POSITION}{PositionCode}#{InstanceId}";
        bool sent = await _client.SendMessageAsync(command);
        if (sent)
        {
            StatusChanged?.Invoke(this, $"Position transmise au serveur : Juge {PositionCode} (Score: {CurrentScore:F1})");
        }
        else
        {
            StatusChanged?.Invoke(this, $"Échec d'envoi de la position Juge {PositionCode}.");
        }
        return sent;
    }

    /// <summary>
    /// Définit la position du juge (Groupe A/B/C pour Taolu ou numéro de juge pour Sanda).
    /// </summary>
    public async Task<bool> SetPositionAsync(string group, int position)
    {
        JudgeGroup = group.Trim().ToUpperInvariant();
        JudgePosition = position;

        // Réinitialise le score de départ en fonction du groupe Taolu
        if (JudgeGroup == "A") CurrentScore = Constants.DEFAULT_SCORE_GROUP_A;
        else if (JudgeGroup == "B") CurrentScore = Constants.DEFAULT_SCORE_GROUP_B;
        else if (JudgeGroup == "C") CurrentScore = Constants.DEFAUTL_SCORE_GROUP_C;
        else CurrentScore = 10.0;

        return await SendCurrentPositionAsync();
    }

    /// <summary>
    /// Demande les données de la compétition au serveur.
    /// </summary>
    public async Task<bool> RequestCompetitionAsync()
    {
        return await _client.SendMessageAsync(Constants.Message.GET_COMPETITION);
    }

    /// <summary>
    /// Demande les informations du match courant au serveur.
    /// </summary>
    public async Task<bool> RequestMatchAsync()
    {
        return await _client.SendMessageAsync(Constants.Message.GET_MATCH);
    }

    /// <summary>
    /// Applique une pénalité par code de mouvement Taolu (ex: "01", "30", "50").
    /// </summary>
    public async Task<bool> ApplyTaoluCodeAsync(string codeOrNumber)
    {
        string search = codeOrNumber.Trim().ToUpperInvariant();
        var mvt = MvtCodeModel.DefaultCodes.FirstOrDefault(c =>
            string.Equals(c.Code, search, StringComparison.OrdinalIgnoreCase))
            ?? new MvtCodeModel
            {
                Code = search,
                Movement = $"Code {search}",
                Value = 0.1,
                Category = "Taolu"
            };

        double deduction = Math.Abs(mvt.Value);
        CurrentScore = Math.Round(Math.Max(0, CurrentScore - deduction), 2);

        var mvtTimeCode = new MvtTimeCodeModel
        {
            Date = DateTime.Now,
            Code = mvt,
            Score = CurrentScore,
            IsSynced = true
        };

        DeductionsHistory.Insert(0, mvtTimeCode);

        var scoreModel = new TransfertScoreModel
        {
            Score = CurrentScore,
            CompetitorId = CurrentMatch?.Competitor1Id,
            Code = mvtTimeCode,
            JudgePosition = PositionCode
        };

        string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
        string message = $"{Constants.Message.JUDGE_SCORE}{json}";

        bool sent = await _client.SendMessageAsync(message);
        if (sent)
        {
            StatusChanged?.Invoke(this, $"[DÉDUCTION CODE] {mvt.Code} - {mvt.Movement} (-{deduction:F1}) => Nouveau score: {CurrentScore:F2}");
        }
        return sent;
    }

    /// <summary>
    /// Déduit directement un nombre de points arbitraire (ex: 0.1, 0.2, 0.5).
    /// </summary>
    public async Task<bool> DeductPointsAsync(double points, string? reason = null)
    {
        double deduction = Math.Abs(points);
        CurrentScore = Math.Round(Math.Max(0, CurrentScore - deduction), 2);

        var mvt = new MvtCodeModel
        {
            Code = $"-{deduction:F1}",
            Movement = reason ?? "Déduction directe",
            Value = deduction,
            Category = "Taolu"
        };

        var mvtTimeCode = new MvtTimeCodeModel
        {
            Date = DateTime.Now,
            Code = mvt,
            Score = CurrentScore,
            IsSynced = true
        };

        DeductionsHistory.Insert(0, mvtTimeCode);

        var scoreModel = new TransfertScoreModel
        {
            Score = CurrentScore,
            CompetitorId = CurrentMatch?.Competitor1Id,
            Code = mvtTimeCode,
            JudgePosition = PositionCode
        };

        string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
        string message = $"{Constants.Message.JUDGE_SCORE}{json}";

        bool sent = await _client.SendMessageAsync(message);
        if (sent)
        {
            StatusChanged?.Invoke(this, $"[DÉDUCTION DIRECTE] -{deduction:F1} ({reason ?? "Saisie"}) => Score: {CurrentScore:F2}");
        }
        return sent;
    }

    /// <summary>
    /// Définit un score absolu (ex: 4.85).
    /// </summary>
    public async Task<bool> SetAbsoluteScoreAsync(double score)
    {
        CurrentScore = Math.Round(Math.Max(0, score), 2);

        var scoreModel = new TransfertScoreModel
        {
            Score = CurrentScore,
            CompetitorId = CurrentMatch?.Competitor1Id,
            JudgePosition = PositionCode
        };

        string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
        string message = $"{Constants.Message.JUDGE_SCORE}{json}";

        bool sent = await _client.SendMessageAsync(message);
        if (sent)
        {
            StatusChanged?.Invoke(this, $"[SCORE FIXÉ] {CurrentScore:F2}");
        }
        return sent;
    }

    /// <summary>
    /// Ajoute 1 point au coin Rouge ou Bleu (Sanda).
    /// </summary>
    public async Task<bool> AddSandaPointAsync(Constants.Side side)
    {
        int? compId = null;
        if (side == Constants.Side.Red)
        {
            RedPoints++;
            compId = CurrentMatch?.Competitor1Id;
        }
        else
        {
            BluePoints++;
            compId = CurrentMatch?.Competitor2Id;
        }

        var scoreModel = new TransfertScoreModel
        {
            Score = 1.0,
            Side = side,
            CompetitorId = compId,
            JudgePosition = PositionCode
        };

        string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
        string message = $"{Constants.Message.JUDGE_SCORE}{json}";

        bool sent = await _client.SendMessageAsync(message);
        if (sent)
        {
            StatusChanged?.Invoke(this, $"[SANDA POINT] +1 pour {side} (Total: Rouge={RedPoints:F0}, Bleu={BluePoints:F0})");
        }
        return sent;
    }

    /// <summary>
    /// Envoie un PING vers le serveur et mesure la latence.
    /// </summary>
    public async Task<bool> PingAsync()
    {
        _pingStopwatch = Stopwatch.StartNew();
        return await _client.SendMessageAsync(Constants.Message.PING);
    }

    /// <summary>
    /// Déconnecte le juge de sa position auprès du serveur.
    /// </summary>
    public async Task<bool> DisconnectJudgeAsync()
    {
        return await _client.SendMessageAsync($"{Constants.Message.JUDGE_DISCONNECT}:{PositionCode}#{InstanceId}");
    }

    /// <summary>
    /// Envoie n'importe quel message brut du protocole.
    /// </summary>
    public async Task<bool> SendRawAsync(string rawMessage)
    {
        return await _client.SendMessageAsync(rawMessage);
    }

    /// <summary>
    /// Active le watchdog keep-alive automatique (ping toutes les X secondes).
    /// </summary>
    public void StartWatchdog(int intervalSeconds = 4)
    {
        StopWatchdog();
        _watchdogCts = new CancellationTokenSource();
        var token = _watchdogCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token);
                    if (_client.IsConnected)
                    {
                        await PingAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        }, token);

        StatusChanged?.Invoke(this, $"Watchdog de connexion activé (toutes les {intervalSeconds}s).");
    }

    /// <summary>
    /// Arrête le watchdog.
    /// </summary>
    public void StopWatchdog()
    {
        if (_watchdogCts != null)
        {
            _watchdogCts.Cancel();
            _watchdogCts.Dispose();
            _watchdogCts = null;
            StatusChanged?.Invoke(this, "Watchdog de connexion arrêté.");
        }
    }

    private void OnBluetoothMessageReceived(object? sender, string message)
    {
        try
        {
            if (message.StartsWith(Constants.Message.MATCH_INFO))
            {
                string json = message.Substring(Constants.Message.MATCH_INFO.Length);
                var matchData = JsonSerializer.Deserialize<MatchInfoData>(json, _jsonOptions);

                if (matchData != null)
                {
                    CurrentMatch = new MatchDisplayInfo
                    {
                        Id = matchData.Id,
                        Type = matchData.Type,
                        Competitor1Id = matchData.Id
                    };

                    // Réinitialisation des déductions pour un nouveau match
                    DeductionsHistory.Clear();
                    RedPoints = 0;
                    BluePoints = 0;

                    if (JudgeGroup == "A") CurrentScore = Constants.DEFAULT_SCORE_GROUP_A;
                    else if (JudgeGroup == "B") CurrentScore = Constants.DEFAULT_SCORE_GROUP_B;
                    else if (JudgeGroup == "C") CurrentScore = Constants.DEFAUTL_SCORE_GROUP_C;

                    MatchUpdated?.Invoke(this, CurrentMatch);
                    NotificationReceived?.Invoke(this, $"Nouveau match reçu : #{matchData.Id} ({matchData.Type})");
                }
            }
            else if (message.StartsWith(Constants.Message.TIMER_SET))
            {
                TimerDisplay = message.Substring(Constants.Message.TIMER_SET.Length);
                TimerUpdated?.Invoke(this, TimerDisplay);
            }
            else if (message == Constants.Message.TIMER_START)
            {
                IsTimerRunning = true;
                TimerUpdated?.Invoke(this, TimerDisplay);
                NotificationReceived?.Invoke(this, "Chronomètre DÉMARRÉ.");
            }
            else if (message == Constants.Message.TIMER_PAUSE)
            {
                IsTimerRunning = false;
                TimerUpdated?.Invoke(this, TimerDisplay);
                NotificationReceived?.Invoke(this, "Chronomètre EN PAUSE.");
            }
            else if (message == Constants.Message.TIMER_STOP)
            {
                IsTimerRunning = false;
                TimerDisplay = "00:00";
                TimerUpdated?.Invoke(this, TimerDisplay);
                NotificationReceived?.Invoke(this, "Chronomètre ARRÊTÉ.");
            }
            else if (message == Constants.Message.PONG)
            {
                TimeSpan latency = _pingStopwatch?.Elapsed ?? TimeSpan.Zero;
                PongReceived?.Invoke(this, latency);
            }
            else if (message.StartsWith(Constants.Message.COMPETITION_DATA))
            {
                string json = message.Substring(Constants.Message.COMPETITION_DATA.Length);
                CurrentCompetition = JsonSerializer.Deserialize<CompetitionModel>(json, _jsonOptions);
                NotificationReceived?.Invoke(this, $"Compétition reçue : {CurrentCompetition?.Name ?? "Inconnue"} ({CurrentCompetition?.Type})");

                // Si la compétition est Taolu et que le juge n'a pas encore spécifié de groupe, affecter par défaut Groupe A
                if (CurrentCompetition?.Type == CompetitionType.Taolu && string.IsNullOrEmpty(JudgeGroup))
                {
                    JudgeGroup = "A";
                    CurrentScore = Constants.DEFAULT_SCORE_GROUP_A;
                    _ = SendCurrentPositionAsync();
                }
            }
            else if (message == Constants.Message.SERVER_STOPPED)
            {
                StopWatchdog();
                StatusChanged?.Invoke(this, "ALERTE : Le serveur d'arbitrage s'est arrêté. Déconnexion et arrêt de l'écoute.");
                NotificationReceived?.Invoke(this, "ALERTE : Le serveur d'arbitrage a annoncé son arrêt volontaire. Écoute stoppée.");
                _ = Task.Run(async () =>
                {
                    try { await _client.DisconnectAsync(); } catch { }
                });
                ServerStopped?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                NotificationReceived?.Invoke(this, $"Message reçu du serveur : {message}");
            }
        }
        catch (Exception ex)
        {
            NotificationReceived?.Invoke(this, $"Erreur traitement message serveur : {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopWatchdog();
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
