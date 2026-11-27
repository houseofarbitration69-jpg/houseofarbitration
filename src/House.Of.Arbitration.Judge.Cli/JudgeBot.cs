using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Judge.Cli;

/// <summary>
/// Robot simulant le comportement d'un juge arbitre pour les tests automatisés.
/// Émet automatiquement des points en Sanda (ou déductions en Taolu) lorsque le chronomètre est actif,
/// avec prise en compte du multiplicateur d'accélération du temps (x1, x2, x4, x6, x8).
/// </summary>
public class JudgeBot : IDisposable
{
    private readonly JudgeSession _session;
    private CancellationTokenSource? _botCts;
    private Task? _botTask;

    public JudgeSession Session => _session;
    public bool IsRunning => _botTask != null && !_botTask.IsCompleted && _botCts != null && !_botCts.IsCancellationRequested;

    /// <summary>
    /// Multiplicateur d'accélération de la vitesse (1, 2, 4, 6, 8).
    /// Réduit proportionnellement l'intervalle entre les attributions de points.
    /// </summary>
    public int SpeedMultiplier { get; set; } = 1;

    /// <summary>
    /// Stratégie de notation : "balanced" (équilibré), "red" (favorise Rouge), "blue" (favorise Bleu), "random" (aléatoire).
    /// </summary>
    public string Strategy { get; set; } = "balanced";

    public int MinIntervalMs { get; set; } = 1500;
    public int MaxIntervalMs { get; set; } = 4000;
    public int TotalScoresSent { get; private set; }

    public event EventHandler<string>? BotActivity;

    public JudgeBot(JudgeSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Démarre le robot de simulation.
    /// </summary>
    public void Start(string? strategy = null, int? speedMultiplier = null)
    {
        if (IsRunning) return;

        if (!string.IsNullOrWhiteSpace(strategy))
        {
            Strategy = strategy.Trim().ToLowerInvariant();
        }

        if (speedMultiplier.HasValue && speedMultiplier.Value > 0)
        {
            SpeedMultiplier = speedMultiplier.Value;
        }

        _botCts = new CancellationTokenSource();
        _botTask = Task.Run(() => BotLoopAsync(_botCts.Token));
        BotActivity?.Invoke(this, $"🤖 Robot Juge [{_session.PositionCode}] DÉMARRÉ (Stratégie: {Strategy}, Vitesse: x{SpeedMultiplier})");
    }

    /// <summary>
    /// Arrête le robot de simulation.
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;

        try
        {
            _botCts?.Cancel();
            _botTask?.Wait(300);
        }
        catch { }
        finally
        {
            _botCts?.Dispose();
            _botCts = null;
            _botTask = null;
        }

        BotActivity?.Invoke(this, $"🤖 Robot Juge [{_session.PositionCode}] ARRÊTÉ (Total notes envoyées : {TotalScoresSent})");
    }

    private async Task BotLoopAsync(CancellationToken token)
    {
        bool wasActive = false;

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Vérifier si on peut noter (chrono actif en Sanda, ou passage ouvert en Taolu)
                if (_session.CanSendScore)
                {
                    if (!wasActive)
                    {
                        wasActive = true;
                        bool isTaolu = _session.CurrentMatch?.Type == RoundType.Order || _session.CurrentCompetition?.Type == CompetitionType.Taolu;
                        string reason = isTaolu ? "Passage ouvert" : "Chronomètre actif";
                        BotActivity?.Invoke(this, $"🤖 [ROBOT {_session.PositionCode}] {reason} ! Début de la simulation (Stratégie: {Strategy}, Vitesse: x{SpeedMultiplier}).");
                    }

                    int speed = Math.Clamp(SpeedMultiplier, 1, 8);
                    int baseInterval = Random.Shared.Next(MinIntervalMs, MaxIntervalMs);
                    int actualInterval = Math.Max(120, baseInterval / speed);

                    await Task.Delay(actualInterval, token);
                    if (token.IsCancellationRequested) break;

                    if (!_session.CanSendScore)
                    {
                        continue;
                    }

                    if (_session.CurrentMatch == null || _session.CurrentMatch.Type != RoundType.Order)
                    {
                        // Sanda : Attribution de points coin Rouge ou Bleu
                        Constants.Side chosenSide;
                        double score = 1.0;

                        switch (Strategy)
                        {
                            case "red":
                                chosenSide = (Random.Shared.Next(100) < 75) ? Constants.Side.Red : Constants.Side.Blue;
                                break;
                            case "blue":
                                chosenSide = (Random.Shared.Next(100) < 75) ? Constants.Side.Blue : Constants.Side.Red;
                                break;
                            case "random":
                                chosenSide = (Random.Shared.Next(2) == 0) ? Constants.Side.Red : Constants.Side.Blue;
                                if (Random.Shared.Next(10) < 3) score = 2.0;
                                break;
                            case "balanced":
                            default:
                                chosenSide = (Random.Shared.Next(2) == 0) ? Constants.Side.Red : Constants.Side.Blue;
                                break;
                        }

                        bool ok = await _session.AddSandaPointAsync(chosenSide, score);
                        if (ok)
                        {
                            TotalScoresSent++;
                            BotActivity?.Invoke(this, $"🤖 [ROBOT {_session.PositionCode}] Point attribué : +{score:F0} {chosenSide} (Total {TotalScoresSent})");
                        }
                    }
                    else
                    {
                        // Taolu : Envoi périodique de codes/déductions réalistes selon le groupe (A, B, C)
                        double minFloor = _session.JudgeGroup switch
                        {
                            "A" => 4.2,
                            "B" => 2.2,
                            "C" => 1.4,
                            _ => 4.0
                        };

                        if (_session.CurrentScore > minFloor && Random.Shared.Next(100) < 65)
                        {
                            bool ok;
                            if (_session.JudgeGroup == "A" && MvtCodeModel.DefaultCodes.Count > 0)
                            {
                                var code = MvtCodeModel.DefaultCodes[Random.Shared.Next(MvtCodeModel.DefaultCodes.Count)];
                                ok = await _session.ApplyTaoluCodeAsync(code.Code);
                            }
                            else
                            {
                                ok = await _session.DeductPointsAsync(0.1, "Simulation Taolu");
                            }

                            if (ok)
                            {
                                TotalScoresSent++;
                                BotActivity?.Invoke(this, $"🤖 [ROBOT {_session.PositionCode}] Taolu note mise à jour : {_session.CurrentScore:F2} (Total {TotalScoresSent})");
                            }
                        }
                    }
                }
                else
                {
                    if (wasActive)
                    {
                        wasActive = false;
                        BotActivity?.Invoke(this, $"🤖 [ROBOT {_session.PositionCode}] Notation fermée ou en pause. En attente...");
                    }

                    // Attendre l'ouverture de la notation
                    await Task.Delay(200, token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                BotActivity?.Invoke(this, $"[ERREUR ROBOT {_session.PositionCode}] {ex.Message}");
                await Task.Delay(500, token);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
