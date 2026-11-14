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
    private CancellationTokenSource? _autoReconnectCts;
    private int _isReconnectingFlag = 0;
    private DateTime _lastServerContactTime = DateTime.UtcNow;
    private int _consecutiveFailedPings = 0;

    private readonly List<TransfertScoreModel> _unsyncedScores = new();

    public WindowsBluetoothClient Client => _client;

    public string InstanceId { get; } = Guid.NewGuid().ToString("N").Substring(0, 6);

    public string JudgeGroup { get; private set; } = string.Empty;
    public int JudgePosition { get; private set; } = 1;
    public string PositionCode => !string.IsNullOrEmpty(JudgeGroup) ? $"{JudgeGroup}_{JudgePosition}" : JudgePosition.ToString();

    public double CurrentScore { get; set; } = 10.0;
    public double RedPoints { get; set; } = 0.0;
    public double BluePoints { get; set; } = 0.0;

    public bool HasUnsyncedChanges => _unsyncedScores.Count > 0;
    public IReadOnlyList<TransfertScoreModel> UnsyncedScores => _unsyncedScores.AsReadOnly();
    public bool IsAutoReconnecting => _autoReconnectCts != null && !_autoReconnectCts.IsCancellationRequested;

    public MatchDisplayInfo? CurrentMatch { get; private set; }
    public CompetitionModel? CurrentCompetition { get; private set; }
    public string ServerName { get; private set; } = string.Empty;
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
        _client.Connected += async (s, e) =>
        {
            await HandleReconnectedAsync(e);
        };
        _client.Disconnected += (s, e) =>
        {
            StopWatchdog();
            StatusChanged?.Invoke(this, $"Déconnecté du serveur [{e}]. Mode hors-ligne actif : vos notes seront mémorisées.");
            // Ne démarrer la reconnexion automatique que si HandleReconnectedAsync n'est pas en cours
            if (_isHandlingReconnection == 0)
            {
                StartAutoReconnect();
            }
        };

        // Reconnexion radio BLE (Windows a rétabli la liaison sans ConnectAsync de notre côté).
        // Cas typique : l'utilisateur réactive le Bluetooth → Windows restaure automatiquement la
        // subscription GATT (le serveur voit le juge connecté), mais le juge a _characteristic=null
        // car DisconnectAsync l'a mise à null lors de la coupure.
        _client.RadioReconnected += (s, addr) =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            StatusChanged?.Invoke(this, $"[{timestamp}] [RADIO-BLE] Reconnexion radio détectée vers [{addr}].");

            // Cas 1 : juge déjà connecté (IsConnected=true) → réinitialiser les compteurs watchdog
            if (_client.IsConnected)
            {
                StatusChanged?.Invoke(this, $"[{timestamp}] [RADIO-BLE] Connexion applicative active — réinitialisation des compteurs watchdog.");
                _lastServerContactTime = DateTime.UtcNow;
                _consecutiveFailedPings = 0;
                if (IsWatchdogActive)
                {
                    StartWatchdog(); // redémarre proprement
                }
                return;
            }

            // Cas 2 : juge déconnecté, mais la boucle autoReconnect tourne déjà.
            // NE PAS lancer un ReconnectAsync concurrent — les deux appellent StartScan()
            // simultanément, ce qui se sabote mutuellement (StopScan tue le scan de l'autre).
            if (IsAutoReconnecting)
            {
                StatusChanged?.Invoke(this, $"[{timestamp}] [RADIO-BLE] AutoReconnect déjà actif — pas de reconnexion concurrente.");
                return;
            }

            // Cas 3 : juge déconnecté, autoReconnect PAS en cours → le démarrer maintenant
            StatusChanged?.Invoke(this, $"[{timestamp}] [RADIO-BLE] Juge hors-ligne, autoReconnect inactif → démarrage reconnexion automatique.");
            if (_isHandlingReconnection == 0)
            {
                StartAutoReconnect();
            }
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

        bool isConnected = _client.IsConnected;
        var mvtTimeCode = new MvtTimeCodeModel
        {
            Date = DateTime.Now,
            Code = mvt,
            Score = CurrentScore,
            IsSynced = isConnected
        };

        DeductionsHistory.Insert(0, mvtTimeCode);

        var scoreModel = new TransfertScoreModel
        {
            Score = CurrentScore,
            CompetitorId = CurrentMatch?.Competitor1Id,
            Code = mvtTimeCode,
            JudgePosition = PositionCode
        };

        if (!isConnected)
        {
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Déduction {mvt.Code} - {mvt.Movement} (-{deduction:F1}) mise en attente (Score: {CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }

        try
        {
            string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
            string message = $"{Constants.Message.JUDGE_SCORE}{json}";

            bool sent = await _client.SendMessageAsync(message);
            if (sent)
            {
                StatusChanged?.Invoke(this, $"[DÉDUCTION CODE] {mvt.Code} - {mvt.Movement} (-{deduction:F1}) => Nouveau score: {CurrentScore:F2}");
                return true;
            }

            mvtTimeCode.IsSynced = false;
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Échec d'envoi BLE, déduction mise en attente (Score: {CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }
        catch (Exception ex)
        {
            mvtTimeCode.IsSynced = false;
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Erreur d'envoi ({ex.Message}), déduction mise en attente (Score: {CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }
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

        bool isConnected = _client.IsConnected;
        var mvtTimeCode = new MvtTimeCodeModel
        {
            Date = DateTime.Now,
            Code = mvt,
            Score = CurrentScore,
            IsSynced = isConnected
        };

        DeductionsHistory.Insert(0, mvtTimeCode);

        var scoreModel = new TransfertScoreModel
        {
            Score = CurrentScore,
            CompetitorId = CurrentMatch?.Competitor1Id,
            Code = mvtTimeCode,
            JudgePosition = PositionCode
        };

        if (!isConnected)
        {
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Déduction directe -{deduction:F1} mise en attente (Score: {CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }

        try
        {
            string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
            string message = $"{Constants.Message.JUDGE_SCORE}{json}";

            bool sent = await _client.SendMessageAsync(message);
            if (sent)
            {
                StatusChanged?.Invoke(this, $"[DÉDUCTION DIRECTE] -{deduction:F1} ({reason ?? "Saisie"}) => Score: {CurrentScore:F2}");
                return true;
            }

            mvtTimeCode.IsSynced = false;
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Échec d'envoi BLE, déduction mise en attente (Score: {CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }
        catch (Exception ex)
        {
            mvtTimeCode.IsSynced = false;
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Erreur d'envoi ({ex.Message}), déduction mise en attente (Score: {CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }
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

        if (!_client.IsConnected)
        {
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Score fixé à {CurrentScore:F2} mis en attente ({_unsyncedScores.Count} en attente)");
            return true;
        }

        try
        {
            string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
            string message = $"{Constants.Message.JUDGE_SCORE}{json}";

            bool sent = await _client.SendMessageAsync(message);
            if (sent)
            {
                StatusChanged?.Invoke(this, $"[SCORE FIXÉ] {CurrentScore:F2}");
                return true;
            }

            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Échec d'envoi BLE, score mis en attente ({CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }
        catch (Exception ex)
        {
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Erreur d'envoi ({ex.Message}), score mis en attente ({CurrentScore:F2} | {_unsyncedScores.Count} en attente)");
            return true;
        }
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

        if (!_client.IsConnected)
        {
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] +1 pour {side} mis en attente (Total: Rouge={RedPoints:F0}, Bleu={BluePoints:F0} | {_unsyncedScores.Count} note(s) en attente)");
            return true;
        }

        try
        {
            string json = JsonSerializer.Serialize(scoreModel, _jsonOptions);
            string message = $"{Constants.Message.JUDGE_SCORE}{json}";

            bool sent = await _client.SendMessageAsync(message);
            if (sent)
            {
                StatusChanged?.Invoke(this, $"[SANDA POINT] +1 pour {side} (Total: Rouge={RedPoints:F0}, Bleu={BluePoints:F0})");
                return true;
            }

            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Échec d'envoi BLE, point mis en attente (Total: Rouge={RedPoints:F0}, Bleu={BluePoints:F0} | {_unsyncedScores.Count} note(s) en attente)");
            return true;
        }
        catch (Exception ex)
        {
            _unsyncedScores.Add(scoreModel);
            StatusChanged?.Invoke(this, $"[MODE HORS-LIGNE] Erreur d'envoi ({ex.Message}), point mis en attente (Total: Rouge={RedPoints:F0}, Bleu={BluePoints:F0} | {_unsyncedScores.Count} note(s) en attente)");
            return true;
        }
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

    private int _isHandlingReconnection = 0;

    /// <summary>
    /// Active le watchdog keep-alive automatique (ping toutes les X secondes).
    /// </summary>
    public void StartWatchdog(int intervalSeconds = 4)
    {
        StopWatchdog();
        _lastServerContactTime = DateTime.UtcNow;
        _consecutiveFailedPings = 0;
        _watchdogCts = new CancellationTokenSource();
        var token = _watchdogCts.Token;
        int tickCount = 0;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token);

                    if (token.IsCancellationRequested) break;

                    tickCount++;
                    string ts = DateTime.Now.ToString("HH:mm:ss.fff");
                    double secondsSinceContact = (DateTime.UtcNow - _lastServerContactTime).TotalSeconds;
                    bool isConn = _client.IsConnected;

                    StatusChanged?.Invoke(this, $"[{ts}] [WATCHDOG-TICK#{tickCount}] IsConnected={isConn} | consecutiveFails={_consecutiveFailedPings} | secondsSinceContact={secondsSinceContact:F1}s");

                    if (DateTime.UtcNow - _lastServerContactTime > TimeSpan.FromSeconds(20))
                    {
                        StatusChanged?.Invoke(this, $"[{ts}] Expiration du heartbeat serveur (>20s sans réponse). Déconnexion.");
                        StopWatchdog();
                        await _client.DisconnectAsync(reason: "Heartbeat expiré (>20s sans réponse serveur)");
                        continue;
                    }

                    if (isConn)
                    {
                        bool sent = await PingAsync();
                        string ts2 = DateTime.Now.ToString("HH:mm:ss.fff");
                        if (!sent)
                        {
                            _consecutiveFailedPings++;
                            StatusChanged?.Invoke(this, $"[{ts2}] [WATCHDOG] Échec ping ({_consecutiveFailedPings}/3).");
                            if (_consecutiveFailedPings >= 3)
                            {
                                StatusChanged?.Invoke(this, $"[{ts2}] [WATCHDOG] 3 pings échoués consécutivement. Déconnexion forcée.");
                                StopWatchdog();
                                await _client.DisconnectAsync(reason: "3 pings consécutifs échoués");
                            }
                        }
                        else
                        {
                            _consecutiveFailedPings = 0;
                            _lastServerContactTime = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        // IsConnected est false sans que DisconnectAsync ait été appelé par le watchdog.
                        // Cela ne devrait pas arriver normalement (IsConnected = _characteristic != null).
                        StatusChanged?.Invoke(this, $"[{ts}] [WATCHDOG] ANOMALIE — IsConnected=false sans déconnexion explicite ! _isHandlingReconnection={_isHandlingReconnection} IsAutoReconnecting={IsAutoReconnecting}");

                        if (_isHandlingReconnection == 0 && !IsAutoReconnecting)
                        {
                            StopWatchdog();
                            StatusChanged?.Invoke(this, "[WATCHDOG] Connexion perdue, démarrage de la reconnexion automatique.");
                            StartAutoReconnect();
                        }
                        else
                        {
                            // Reconnexion déjà en cours — arrêter le watchdog
                            StopWatchdog();
                        }
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
            _consecutiveFailedPings = 0;
            StatusChanged?.Invoke(this, "Watchdog de connexion arrêté.");
        }
    }

    /// <summary>
    /// Démarre la boucle de reconnexion automatique en arrière-plan lorsque le signal est perdu.
    /// </summary>
    public void StartAutoReconnect(int intervalSeconds = 2)
    {
        if (IsAutoReconnecting) return;

        // Guard : si le client est déjà connecté, ne pas démarrer la reconnexion
        if (_client.IsConnected) return;

        _autoReconnectCts = new CancellationTokenSource();
        var token = _autoReconnectCts.Token;

        _ = Task.Run(async () =>
        {
            StatusChanged?.Invoke(this, "Reconnexion automatique activée (recherche permanente du serveur)...");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Vérification en début de chaque itération : si déjà connecté, arrêter
                    if (_client.IsConnected)
                    {
                        StatusChanged?.Invoke(this, "[RECONNEXION] Connexion détectée, arrêt de la reconnexion automatique.");
                        StopAutoReconnect();
                        break;
                    }

                    if (Interlocked.CompareExchange(ref _isReconnectingFlag, 1, 0) == 0)
                    {
                        try
                        {
                            bool reconnected = await _client.ReconnectAsync();
                            if (reconnected)
                            {
                                break;
                            }
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _isReconnectingFlag, 0);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        }, token);
    }

    /// <summary>
    /// Arrête la reconnexion automatique.
    /// </summary>
    public void StopAutoReconnect()
    {
        if (_autoReconnectCts != null)
        {
            _autoReconnectCts.Cancel();
            _autoReconnectCts.Dispose();
            _autoReconnectCts = null;
        }
    }

    /// <summary>
    /// Gère la reconnexion au serveur : réenregistrement de la position,
    /// synchronisation des notes prises hors-ligne, et mise à jour du match.
    /// </summary>
    public async Task HandleReconnectedAsync(string address)
    {
        if (Interlocked.CompareExchange(ref _isHandlingReconnection, 1, 0) != 0)
        {
            StatusChanged?.Invoke(this, "[RECONNEXION] Traitement déjà en cours, ignoré.");
            return;
        }

        try
        {
            string tsStart = DateTime.Now.ToString("HH:mm:ss.fff");
            // Arrêter immédiatement la reconnexion automatique AVANT tout await,
            // pour éviter qu'une itération concurrente relance ConnectAsync
            StopAutoReconnect();
            StopWatchdog();

            _lastServerContactTime = DateTime.UtcNow;
            _consecutiveFailedPings = 0;

            StatusChanged?.Invoke(this, $"[{tsStart}] [RECONNEXION RÉUSSIE] Connecté au serveur [{address}] ! Réenregistrement...");

            // 1. Réenregistrer la position auprès du serveur
            await SendCurrentPositionAsync();
            await Task.Delay(150);

            // 2. Envoyer toutes les notes accumulées hors-ligne
            if (_unsyncedScores.Count > 0 || DeductionsHistory.Any(d => !d.IsSynced))
            {
                await SyncUnsyncedScoresAsync();
            }

            // 3. Demander les données courantes du match et de la compétition
            await Task.Delay(100);
            await RequestMatchAsync();
            await RequestCompetitionAsync();

            // 4. Démarrer le watchdog seulement si on est encore connecté à l'issue du traitement
            string tsEnd = DateTime.Now.ToString("HH:mm:ss.fff");
            bool stillConnected = _client.IsConnected;
            StatusChanged?.Invoke(this, $"[{tsEnd}] [RECONNEXION] Post-traitement terminé. IsConnected={stillConnected}");

            if (stillConnected)
            {
                StartWatchdog();
            }
            else
            {
                StatusChanged?.Invoke(this, $"[{tsEnd}] [RECONNEXION] Connexion perdue pendant le traitement post-reconnexion. Reprise de la reconnexion automatique.");
                StartAutoReconnect();
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erreur lors de la post-reconnexion : {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isHandlingReconnection, 0);
        }
    }

    /// <summary>
    /// Envoie au serveur toutes les notes accumulées pendant la déconnexion ainsi que l'état global.
    /// </summary>
    public async Task SyncUnsyncedScoresAsync()
    {
        if (!_client.IsConnected) return;

        int pendingCount = _unsyncedScores.Count;
        StatusChanged?.Invoke(this, $"Synchronisation de {pendingCount} note(s) hors-ligne vers le serveur...");

        try
        {
            if (_unsyncedScores.Count > 0)
            {
                var scoresToSend = _unsyncedScores.ToList();
                foreach (var score in scoresToSend)
                {
                    if (string.IsNullOrEmpty(score.JudgePosition))
                    {
                        score.JudgePosition = PositionCode;
                    }

                    string json = JsonSerializer.Serialize(score, _jsonOptions);
                    string message = $"{Constants.Message.JUDGE_SCORE}{json}";

                    bool sent = await _client.SendMessageAsync(message);
                    if (sent)
                    {
                        _unsyncedScores.Remove(score);
                        if (score.Code != null)
                        {
                            score.Code.IsSynced = true;
                        }
                    }

                    await Task.Delay(50);
                }
            }

            foreach (var d in DeductionsHistory)
            {
                d.IsSynced = true;
            }

            // Envoi de l'état global synchronisé pour garantir la cohérence
            await SendSyncStateAsync();

            StatusChanged?.Invoke(this, "Toutes les notes hors-ligne ont été synchronisées avec succès !");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erreur lors de la synchronisation : {ex.Message}");
        }
    }

    /// <summary>
    /// Envoie l'état global du juge (JUDGE_SYNC_STATE) au serveur.
    /// </summary>
    public async Task<bool> SendSyncStateAsync()
    {
        if (!_client.IsConnected) return false;

        var syncState = new JudgeStateModel
        {
            Group = JudgeGroup,
            Number = JudgePosition,
            Score = CurrentScore,
            RedPoints = RedPoints,
            BluePoints = BluePoints,
            Codes = DeductionsHistory.ToList()
        };

        string json = JsonSerializer.Serialize(syncState, _jsonOptions);
        string message = $"{Constants.Message.JUDGE_SYNC_STATE}{json}";
        return await _client.SendMessageAsync(message);
    }

    private void OnBluetoothMessageReceived(object? sender, string message)
    {
        _lastServerContactTime = DateTime.UtcNow;
        _consecutiveFailedPings = 0;

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
                _lastServerContactTime = DateTime.UtcNow;
                _consecutiveFailedPings = 0;
                TimeSpan latency = _pingStopwatch?.Elapsed ?? TimeSpan.Zero;
                PongReceived?.Invoke(this, latency);
            }
            else if (message.StartsWith(Constants.Message.SERVER_NAME))
            {
                ServerName = message.Substring(Constants.Message.SERVER_NAME.Length).Trim();
                NotificationReceived?.Invoke(this, $"Serveur connecté : {ServerName}");
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
                StopAutoReconnect();
                StatusChanged?.Invoke(this, "ALERTE : Le serveur d'arbitrage s'est arrêté. Déconnexion et arrêt de l'écoute.");
                NotificationReceived?.Invoke(this, "ALERTE : Le serveur d'arbitrage a annoncé son arrêt volontaire. Écoute stoppée.");
                _ = Task.Run(async () =>
                {
                    try { await _client.DisconnectAsync(reason: "Serveur arrêté volontairement"); } catch { }
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
        StopAutoReconnect();
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
