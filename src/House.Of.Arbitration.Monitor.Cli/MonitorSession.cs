using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Monitor.Cli;

/// <summary>
/// Session Monitor : se connecte au serveur en BLE et reçoit TOUS les messages.
/// Stocke l'état dans la base locale et expose des méthodes pour envoyer des corrections.
/// </summary>
public class MonitorSession : IDisposable
{
    private readonly WindowsBluetoothClient _client;
    private readonly MonitorDatabase _db;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
    private Stopwatch? _pingStopwatch;
    private CancellationTokenSource? _watchdogCts;

    public WindowsBluetoothClient Client => _client;

    public string InstanceId { get; } = "MONITOR_" + Guid.NewGuid().ToString("N")[..6].ToUpper();

    // ── État courant ──────────────────────────────────────────────────────────

    public MatchInfoData? CurrentMatch { get; private set; }
    public CompetitionModel? CurrentCompetition { get; private set; }
    public string ServerName { get; private set; } = string.Empty;
    public string TimerDisplay { get; private set; } = "02:00";
    public bool IsTimerRunning { get; private set; }
    public bool IsWatchdogActive => _watchdogCts != null && !_watchdogCts.IsCancellationRequested;

    // Compteur de notes reçues depuis connexion
    public int TotalScoresReceived { get; private set; }

    // ── Événements ────────────────────────────────────────────────────────────

    public event EventHandler<MatchInfoData?>? MatchUpdated;
    public event EventHandler<MatchResultModel>? MatchFinished;
    public event EventHandler<CompetitionModel>? CompetitionUpdated;
    public event EventHandler<TransfertScoreModel>? ScoreReceived;
    public event EventHandler<string>? TimerUpdated;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler? ServerStopped;
    public event EventHandler<TimeSpan>? PongReceived;

    public MonitorSession(WindowsBluetoothClient client, MonitorDatabase db)
    {
        _client = client;
        _db = db;

        _client.MessageReceived += OnMessageReceived;
        _client.Connected += async (_, addr) => await OnConnectedAsync(addr);
        _client.Disconnected += (_, reason) =>
        {
            StopWatchdog();
            StatusChanged?.Invoke(this, $"[DÉCONNECTÉ] {reason}");
        };
    }

    // ── Connexion ─────────────────────────────────────────────────────────────

    private async Task OnConnectedAsync(string address)
    {
        StatusChanged?.Invoke(this, $"[CONNECTÉ] Serveur {address}. Enregistrement Monitor...");
        bool reg = await _client.SendMessageAsync(Constants.Message.MONITOR_REGISTER);
        StatusChanged?.Invoke(this, reg
            ? "[MONITOR] MONITOR_REGISTER envoyé."
            : "[ERREUR] Échec envoi MONITOR_REGISTER.");
        await Task.Delay(150);
        await _client.SendMessageAsync(Constants.Message.GET_COMPETITION);
        await Task.Delay(150);
        await _client.SendMessageAsync(Constants.Message.GET_MATCH);
    }

    // ── Réception de messages ─────────────────────────────────────────────────

    private async void OnMessageReceived(object? sender, string message)
    {
        try
        {
            await ProcessMessageAsync(message);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"[ERREUR TRAITEMENT] {ex.Message}");
        }
    }

    public async Task ProcessMessageAsync(string message)
    {
        // PING → PONG
        if (message == Constants.Message.PING)
        {
            await _client.SendMessageAsync(Constants.Message.PONG);
            return;
        }

        // PONG
        if (message == Constants.Message.PONG)
        {
            if (_pingStopwatch != null)
            {
                var latency = _pingStopwatch.Elapsed;
                _pingStopwatch.Stop();
                PongReceived?.Invoke(this, latency);
            }
            return;
        }

        // SERVER_NAME
        if (message.StartsWith(Constants.Message.SERVER_NAME))
        {
            ServerName = message[Constants.Message.SERVER_NAME.Length..];
            StatusChanged?.Invoke(this, $"[SERVEUR] {ServerName}");
            await _db.LogEventAsync("SERVER_NAME", ServerName);
            return;
        }

        // SERVER_STOPPED
        if (message == Constants.Message.SERVER_STOPPED)
        {
            StatusChanged?.Invoke(this, "[SERVEUR ARRÊTÉ]");
            await _db.LogEventAsync("SERVER_STOPPED");
            ServerStopped?.Invoke(this, EventArgs.Empty);
            return;
        }

        // COMPETITION_DATA
        if (message.StartsWith(Constants.Message.COMPETITION_DATA))
        {
            var json = message[Constants.Message.COMPETITION_DATA.Length..];
            var competition = JsonSerializer.Deserialize<CompetitionModel>(json, _jsonOptions);
            if (competition != null)
            {
                CurrentCompetition = competition;
                await _db.SaveCompetitionAsync(competition);
                CompetitionUpdated?.Invoke(this, competition);
                StatusChanged?.Invoke(this, $"[COMPÉTITION] {competition.Name} ({competition.Type}) — {competition.Categories?.Count ?? 0} catégorie(s)");
            }
            return;
        }

        // MATCH_INFO
        if (message.StartsWith(Constants.Message.MATCH_INFO))
        {
            var json = message[Constants.Message.MATCH_INFO.Length..];
            if (json is "{}" or "")
            {
                CurrentMatch = null;
                MatchUpdated?.Invoke(this, null);
                StatusChanged?.Invoke(this, "[MATCH] Aucun match en cours.");
                return;
            }

            var match = JsonSerializer.Deserialize<MatchInfoData>(json, _jsonOptions);
            CurrentMatch = match;
            if (match != null)
            {
                await _db.SaveMatchAsync(match);
                MatchUpdated?.Invoke(this, match);
                string c1 = match.Competitor1?.Name ?? "?";
                string c2 = match.Competitor2?.Name ?? "?";
                StatusChanged?.Invoke(this, $"[MATCH #{match.Id}] {match.CategoryName} | {match.StageName} | {c1} vs {c2}");
            }
            return;
        }

        // MATCH_RESULT
        if (message.StartsWith(Constants.Message.MATCH_RESULT))
        {
            var json = message[Constants.Message.MATCH_RESULT.Length..];
            var result = JsonSerializer.Deserialize<MatchResultModel>(json, _jsonOptions);
            if (result != null)
            {
                await _db.MarkMatchFinishedAsync(result.MatchId, result.WinnerId);
                await _db.LogEventAsync("MATCH_RESULT", json);
                MatchFinished?.Invoke(this, result);
            }
            return;
        }

        // JUDGE_SCORE
        if (message.StartsWith(Constants.Message.JUDGE_SCORE))
        {
            var json = message[Constants.Message.JUDGE_SCORE.Length..];
            var score = JsonSerializer.Deserialize<TransfertScoreModel>(json, _jsonOptions);
            if (score != null)
            {
                TotalScoresReceived++;
                await _db.SaveJudgeScoreAsync(CurrentMatch?.Id, score);
                ScoreReceived?.Invoke(this, score);
            }
            return;
        }

        // JUDGE_STATE
        if (message.StartsWith(Constants.Message.JUDGE_STATE))
        {
            await _db.LogEventAsync("JUDGE_STATE", message[Constants.Message.JUDGE_STATE.Length..]);
            return;
        }

        // JUDGE_SYNC_STATE
        if (message.StartsWith(Constants.Message.JUDGE_SYNC_STATE))
        {
            await _db.LogEventAsync("JUDGE_SYNC_STATE", message[Constants.Message.JUDGE_SYNC_STATE.Length..]);
            return;
        }

        // TIMER
        if (message == Constants.Message.TIMER_START)
        {
            IsTimerRunning = true;
            TimerUpdated?.Invoke(this, TimerDisplay);
            await _db.LogEventAsync("TIMER_START");
            return;
        }
        if (message == Constants.Message.TIMER_PAUSE)
        {
            IsTimerRunning = false;
            TimerUpdated?.Invoke(this, TimerDisplay);
            await _db.LogEventAsync("TIMER_PAUSE");
            return;
        }
        if (message == Constants.Message.TIMER_STOP)
        {
            IsTimerRunning = false;
            TimerUpdated?.Invoke(this, TimerDisplay);
            await _db.LogEventAsync("TIMER_STOP");
            return;
        }
        if (message.StartsWith(Constants.Message.TIMER_SET))
        {
            TimerDisplay = message[Constants.Message.TIMER_SET.Length..];
            TimerUpdated?.Invoke(this, TimerDisplay);
            return;
        }

        // Message inconnu — loggué quand même
        await _db.LogEventAsync("UNKNOWN", message.Length > 500 ? message[..500] : message);
    }

    // ── Corrections ───────────────────────────────────────────────────────────

    /// <summary>
    /// Envoie une correction de vainqueur au serveur.
    /// </summary>
    public async Task<bool> SendWinnerCorrectionAsync(int matchId, Constants.Side winningSide, int? winnerCompetitorId = null, string? reason = null)
    {
        var correction = new WinnerCorrectionModel
        {
            MatchId = matchId,
            WinningSide = winningSide,
            WinnerCompetitorId = winnerCompetitorId,
            Reason = reason
        };
        string json = JsonSerializer.Serialize(correction, _jsonOptions);
        string message = $"{Constants.Message.CORRECTION_WINNER}{json}";

        long logId = await _db.LogCorrectionSentAsync("CORRECTION_WINNER", json);
        bool sent = await _client.SendMessageAsync(message);
        if (sent)
        {
            await _db.MarkCorrectionAckedAsync(logId); // considéré ACK quand l'envoi BLE réussit
            StatusChanged?.Invoke(this, $"[CORRECTION WINNER] Match #{matchId} → {winningSide}" + (reason != null ? $" ({reason})" : ""));
        }
        else
        {
            StatusChanged?.Invoke(this, $"[ERREUR] Impossible d'envoyer la correction de vainqueur pour le match #{matchId}.");
        }
        return sent;
    }

    /// <summary>
    /// Envoie une correction de score d'un juge au serveur.
    /// </summary>
    public async Task<bool> SendScoreCorrectionAsync(int matchId, string judgePosition, Constants.Side? side, int? competitorId, double newScore, int round = 1, string? reason = null)
    {
        var correction = new ScoreCorrectionModel
        {
            MatchId = matchId,
            JudgePosition = judgePosition,
            Side = side,
            CompetitorId = competitorId,
            NewScore = newScore,
            Round = round,
            Reason = reason
        };
        string json = JsonSerializer.Serialize(correction, _jsonOptions);
        string message = $"{Constants.Message.CORRECTION_SCORE}{json}";

        long logId = await _db.LogCorrectionSentAsync("CORRECTION_SCORE", json);
        bool sent = await _client.SendMessageAsync(message);
        if (sent)
        {
            await _db.MarkCorrectionAckedAsync(logId);
            StatusChanged?.Invoke(this, $"[CORRECTION SCORE] Match #{matchId} | Juge {judgePosition} → {newScore:F2}" + (reason != null ? $" ({reason})" : ""));
        }
        else
        {
            StatusChanged?.Invoke(this, $"[ERREUR] Impossible d'envoyer la correction de score pour le match #{matchId}.");
        }
        return sent;
    }

    // ── Utilitaires ───────────────────────────────────────────────────────────

    public async Task<bool> PingAsync()
    {
        _pingStopwatch = Stopwatch.StartNew();
        return await _client.SendMessageAsync(Constants.Message.PING);
    }

    public async Task<bool> RequestCompetitionAsync() =>
        await _client.SendMessageAsync(Constants.Message.GET_COMPETITION);

    public async Task<bool> RequestMatchAsync() =>
        await _client.SendMessageAsync(Constants.Message.GET_MATCH);

    public async Task<bool> SendRawAsync(string raw) =>
        await _client.SendMessageAsync(raw);

    // ── Watchdog ──────────────────────────────────────────────────────────────

    public void StartWatchdog(int intervalSeconds = 5)
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
                    if (token.IsCancellationRequested) break;
                    if (_client.IsConnected)
                    {
                        await PingAsync();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }, token);

        StatusChanged?.Invoke(this, $"Watchdog Monitor activé (toutes les {intervalSeconds}s).");
    }

    public void StopWatchdog()
    {
        if (_watchdogCts != null)
        {
            _watchdogCts.Cancel();
            _watchdogCts.Dispose();
            _watchdogCts = null;
        }
    }

    public void Dispose()
    {
        StopWatchdog();
        _client.MessageReceived -= OnMessageReceived;
    }
}
