using System.Globalization;
using System.Text;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Monitor.Cli;

internal static class Program
{
    private static readonly object ConsoleLock = new();
    private static WindowsBluetoothClient? _client;
    private static MonitorSession? _session;
    private static MonitorDatabase? _db;
    private static bool _running = true;
    private static bool _dumpScores = true;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintBanner();

        // ── Analyse des arguments ─────────────────────────────────────────────
        string? connectTarget = null;
        bool autoScan = false;
        string dbPath = $"monitor_{DateTime.Now:yyyyMMdd_HHmmss}.db";

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            switch (arg)
            {
                case "-h" or "--help" or "/?":
                    PrintHelp();
                    return 0;

                case "-s" or "--scan":
                    autoScan = true;
                    break;

                case "-c" or "--connect" when i + 1 < args.Length:
                    connectTarget = args[++i];
                    break;

                case "--db" when i + 1 < args.Length:
                    dbPath = args[++i];
                    break;

                case "--no-db":
                    dbPath = ":memory:";
                    break;

                case "--dump-scores":
                    _dumpScores = true;
                    break;

                case "--quiet-scores":
                    _dumpScores = false;
                    break;
            }
        }

        // ── Initialisation DB ─────────────────────────────────────────────────
        _db = new MonitorDatabase(dbPath);
        await _db.InitializeAsync();
        LogInfo($"Base de données locale : {(dbPath == ":memory:" ? "[mémoire uniquement]" : Path.GetFullPath(dbPath))}");

        // ── Initialisation BLE ────────────────────────────────────────────────
        _client = new WindowsBluetoothClient();
        _session = new MonitorSession(_client, _db);
        HookSessionEvents(_session, _client);

        if (autoScan)
        {
            await RunScanAsync(5);
        }

        if (!string.IsNullOrEmpty(connectTarget))
        {
            await TryConnectAsync(connectTarget);
        }

        // ── REPL ──────────────────────────────────────────────────────────────
        PrintHelpSummary();

        while (_running)
        {
            PrintPrompt();
            string? line = Console.ReadLine();
            if (line == null) break;

            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            await HandleCommandAsync(line);
        }

        // ── Nettoyage ─────────────────────────────────────────────────────────
        _session.Dispose();
        if (_db != null) await _db.DisposeAsync();
        LogInfo("Monitor CLI fermé.");
        return 0;
    }

    // ── Événements session ────────────────────────────────────────────────────

    private static void HookSessionEvents(MonitorSession session, WindowsBluetoothClient client)
    {
        client.LogMessage += (_, msg) => LogInfo($"[BLE] {msg}");

        session.StatusChanged += (_, msg) => LogInfo(msg);

        //session.PongReceived += (_, latency) => LogColor(ConsoleColor.Cyan, $"[PONG] {latency.TotalMilliseconds:F0} ms");

        session.MatchUpdated += (_, match) =>
        {
            if (match != null)
                LogColor(ConsoleColor.Green, $"[MATCH #{match.Id}] {match.CategoryName} | {match.StageName} | {match.Competitor1?.Name ?? "?"} vs {match.Competitor2?.Name ?? "?"}");
            else
                LogColor(ConsoleColor.Yellow, "🏁 Aucun match en cours.");
        };

        session.MatchFinished += (_, res) =>
        {
            string side = res.WinnerSide.HasValue ? $" ({res.WinnerSide})" : "";
            string reason = !string.IsNullOrWhiteSpace(res.Reason) ? $" — {res.Reason}" : "";
            LogColor(ConsoleColor.Yellow, $"🏆 [MATCH #{res.MatchId} TERMINÉ] Vainqueur : {res.WinnerName ?? $"ID:{res.WinnerId}"}{side}{reason}");
        };

        session.CompetitionUpdated += (_, comp) =>
            LogColor(ConsoleColor.Magenta, $"[COMPÉTITION] {comp.Name} ({comp.Type})");

        session.ScoreReceived += (_, score) =>
        {
            if (_dumpScores)
            {
                string side = score.Side?.ToString() ?? "-";
                LogColor(ConsoleColor.DarkYellow,
                    $"[SCORE] Juge {score.JudgePosition ?? "?"} | Côté: {side} | CompId: {score.CompetitorId} | {score.Score:F2} | Round {score.Round}");
            }
        };

        session.TimerUpdated += (_, timer) =>
            LogColor(ConsoleColor.DarkYellow, $"[CHRONO] {timer} {(session.IsTimerRunning ? "(En cours)" : "(Stop)")}");

        session.ServerStopped += (_, _) =>
        {
            lock (ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n========================================================");
                Console.WriteLine("  SERVEUR ARRÊTÉ — Fermeture du Monitor.");
                Console.WriteLine("========================================================");
                Console.ResetColor();
            }
            _running = false;
        };
    }

    // ── Commandes REPL ────────────────────────────────────────────────────────

    private static async Task HandleCommandAsync(string commandLine)
    {
        string[] parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string cmd = parts[0].ToLowerInvariant();
        string[] cmdArgs = parts.Skip(1).ToArray();

        try
        {
            switch (cmd)
            {
                // ── Connexion ────────────────────────────────────────────────
                case "scan":
                    int timeout = cmdArgs.Length > 0 && int.TryParse(cmdArgs[0], out int t) ? t : 5;
                    await RunScanAsync(timeout);
                    break;

                case "connect" or "c":
                    if (cmdArgs.Length == 0)
                    {
                        if (_client?.LastConnectedBluetoothAddress.HasValue == true)
                        {
                            await _client.ReconnectAsync();
                            return;
                        }
                        LogError("Usage: connect <index_du_scan | adresse_bluetooth>");
                        return;
                    }
                    await TryConnectAsync(cmdArgs[0]);
                    break;

                case "disconnect" or "disc":
                    if (_client != null)
                        await _client.DisconnectAsync();
                    break;

                // ── État ─────────────────────────────────────────────────────
                case "status" or "st":
                    PrintStatus();
                    break;

                case "match" or "m":
                    await _session!.RequestMatchAsync();
                    break;

                case "comp":
                    await _session!.RequestCompetitionAsync();
                    break;

                case "ping":
                    await _session!.PingAsync();
                    break;

                // ── Historique DB ────────────────────────────────────────────
                case "history" or "hist":
                    await ShowHistoryAsync(cmdArgs.Length > 0 && int.TryParse(cmdArgs[0], out int n) ? n : 20);
                    break;

                case "scores":
                    if (cmdArgs.Length == 0 || !int.TryParse(cmdArgs[0], out int matchId))
                    {
                        LogError("Usage: scores <matchId>");
                        return;
                    }
                    await ShowScoresAsync(matchId);
                    break;

                // ── Corrections ──────────────────────────────────────────────
                case "correction" or "corr":
                    await HandleCorrectionAsync(cmdArgs);
                    break;

                // ── Export ───────────────────────────────────────────────────
                case "export":
                    string outDir = cmdArgs.Length > 0 ? cmdArgs[0] : "monitor_export";
                    await _db!.ExportToCsvAsync(outDir);
                    LogSuccess($"Export CSV → {Path.GetFullPath(outDir)}");
                    break;

                // ── Dump scores en direct ────────────────────────────────────
                case "dump":
                    _dumpScores = !_dumpScores;
                    LogInfo($"Dump notes en temps réel : {(_dumpScores ? "ACTIVÉ" : "DÉSACTIVÉ")}");
                    break;

                // ── Watchdog ─────────────────────────────────────────────────
                case "watchdog":
                    if (cmdArgs.Length > 0 && cmdArgs[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                        _session!.StopWatchdog();
                    else
                    {
                        int interval = cmdArgs.Length > 0 && int.TryParse(cmdArgs[0], out int sec) ? sec : 5;
                        _session!.StartWatchdog(interval);
                    }
                    break;

                // ── Brut ─────────────────────────────────────────────────────
                case "raw":
                    if (cmdArgs.Length == 0) { LogError("Usage: raw <message>"); return; }
                    await _session!.SendRawAsync(string.Join(" ", cmdArgs));
                    break;

                // ── UI ───────────────────────────────────────────────────────
                case "clear" or "cls":
                    Console.Clear();
                    PrintBanner();
                    break;

                case "help" or "?":
                    PrintHelp();
                    break;

                case "quit" or "exit" or "q":
                    _running = false;
                    break;

                default:
                    LogError($"Commande inconnue : '{cmd}'. Tapez 'help'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError($"Erreur : {ex.Message}");
        }
    }

    // ── Corrections ───────────────────────────────────────────────────────────

    private static async Task HandleCorrectionAsync(string[] args)
    {
        // correction winner <matchId> <red|blue> [raison...]
        // correction score <matchId> <judgePos> <red|blue> <score> [raison...]
        if (args.Length < 1)
        {
            LogError("Usage:\n  correction winner <matchId> <red|blue> [raison]\n  correction score <matchId> <judgePos> <red|blue> <score> [raison]");
            return;
        }

        string sub = args[0].ToLowerInvariant();

        if (sub == "winner")
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int matchId))
            {
                LogError("Usage: correction winner <matchId> <red|blue> [raison]");
                return;
            }
            string sideStr = args[2].ToLowerInvariant();
            if (sideStr is not ("red" or "rouge" or "blue" or "bleu"))
            {
                LogError("Le côté doit être 'red' (rouge) ou 'blue' (bleu).");
                return;
            }
            var side = sideStr is "red" or "rouge" ? Constants.Side.Red : Constants.Side.Blue;
            string? reason = args.Length > 3 ? string.Join(" ", args.Skip(3)) : null;

            await _session!.SendWinnerCorrectionAsync(matchId, side, null, reason);
        }
        else if (sub == "score")
        {
            // correction score <matchId> <judgePos> <red|blue> <score> [raison]
            if (args.Length < 5 || !int.TryParse(args[1], out int matchId) ||
                !double.TryParse(args[4].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double score))
            {
                LogError("Usage: correction score <matchId> <judgePos> <red|blue> <score> [raison]");
                return;
            }
            string judgePos = args[2];
            string sideStr = args[3].ToLowerInvariant();
            Constants.Side? side = sideStr is "red" or "rouge" ? Constants.Side.Red
                                 : sideStr is "blue" or "bleu" ? Constants.Side.Blue
                                 : null;
            string? reason = args.Length > 5 ? string.Join(" ", args.Skip(5)) : null;

            await _session!.SendScoreCorrectionAsync(matchId, judgePos, side, null, score, 1, reason);
        }
        else
        {
            LogError($"Sous-commande inconnue : '{sub}'. Utilisez 'winner' ou 'score'.");
        }
    }

    // ── Affichage ─────────────────────────────────────────────────────────────

    private static async Task ShowHistoryAsync(int limit)
    {
        var rows = await _db!.GetMatchHistoryAsync(limit);
        if (rows.Count == 0) { LogInfo("Aucun match dans la base locale."); return; }

        LogColor(ConsoleColor.Yellow, $"\n--- HISTORIQUE DES {rows.Count} DERNIERS MATCHS ---");
        foreach (var r in rows)
        {
            string win = r.WinnerId.HasValue ? $" → Gagnant ID:{r.WinnerId}" : "";
            string fin = r.IsFinished ? "[TERMINÉ]" : "[EN COURS]";
            Console.WriteLine($"  #{r.Id,-4} {fin,-12} {r.CategoryName,-25} {r.StageName,-20} {r.Competitor1Name} vs {r.Competitor2Name}{win}");
        }
        Console.WriteLine();
    }

    private static async Task ShowScoresAsync(int matchId)
    {
        var rows = await _db!.GetScoresForMatchAsync(matchId);
        if (rows.Count == 0) { LogInfo($"Aucun score pour le match #{matchId}."); return; }

        LogColor(ConsoleColor.Yellow, $"\n--- SCORES MATCH #{matchId} ({rows.Count} entrée(s)) ---");
        foreach (var r in rows)
        {
            Console.WriteLine($"  Juge {r.JudgePosition,-6} | Côté: {r.Side,-5} | CompID: {r.CompetitorId,-6} | Score: {r.Score,6:F2} | Round: {r.Round} | {r.ReceivedAt}");
        }
        Console.WriteLine();
    }

    private static void PrintStatus()
    {
        LogColor(ConsoleColor.Cyan, "\n--- ÉTAT DU MONITOR ---");
        Console.WriteLine($"  Connexion BLE  : {(_client!.IsConnected ? $"CONNECTÉ ({_client.ConnectedDeviceName ?? _client.ConnectedDeviceAddress})" : "DÉCONNECTÉ")}");
        Console.WriteLine($"  Serveur        : {(_session!.ServerName != string.Empty ? _session.ServerName : "inconnu")}");
        Console.WriteLine($"  Chronomètre    : {_session.TimerDisplay} {(_session.IsTimerRunning ? "[ACTIF]" : "[STOP]")}");
        Console.WriteLine($"  Scores reçus   : {_session.TotalScoresReceived}");
        Console.WriteLine($"  Dump en direct : {(_dumpScores ? "ACTIVÉ" : "DÉSACTIVÉ")}");
        Console.WriteLine($"  Watchdog       : {(_session.IsWatchdogActive ? "ACTIF" : "INACTIF")}");
        if (_session.CurrentMatch != null)
        {
            var m = _session.CurrentMatch;
            Console.WriteLine($"  Match actuel   : #{m.Id} | {m.CategoryName} | {m.StageName}");
            Console.WriteLine($"    Rouge        : {m.Competitor1?.Name ?? "?"} (ID:{m.Competitor1?.Id})");
            Console.WriteLine($"    Bleu         : {m.Competitor2?.Name ?? "?"} (ID:{m.Competitor2?.Id})");
        }
        else
            Console.WriteLine("  Match actuel   : Aucun");
        if (_session.CurrentCompetition != null)
            Console.WriteLine($"  Compétition    : {_session.CurrentCompetition.Name}");
        Console.WriteLine();
    }

    // ── Scan & Connexion ──────────────────────────────────────────────────────

    private static async Task RunScanAsync(int seconds)
    {
        LogInfo($"Scan BLE pendant {seconds}s...");
        var devices = await _client!.ScanForDevicesAsync(TimeSpan.FromSeconds(seconds));
        if (devices.Count == 0)
        {
            LogWarning("Aucun serveur détecté.");
            return;
        }
        LogColor(ConsoleColor.Yellow, $"\n--- {devices.Count} PÉRIPHÉRIQUE(S) TROUVÉ(S) ---");
        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            string marker = d.HasArbitrationService ? " ← [SERVEUR ARBITRATION]" : "";
            Console.WriteLine($"  [{i + 1}] {d.DisplayName} ({d.AddressFormatted}) RSSI: {d.Rssi} dBm{marker}");
        }
        Console.WriteLine();
        LogInfo("Tapez 'connect <index>' pour vous connecter.");
    }

    private static async Task TryConnectAsync(string target)
    {
        if (int.TryParse(target, out int index) && _client!.DiscoveredDevices.Count > 0)
        {
            if (index >= 1 && index <= _client.DiscoveredDevices.Count)
            {
                var device = _client.DiscoveredDevices[index - 1];
                LogInfo($"Connexion à [{device.DisplayName}]...");
                bool ok = await _client.ConnectAsync(device);
                if (ok) { LogSuccess($"Connecté à {device.DisplayName} !"); _session!.StartWatchdog(5); }
                else LogError($"Échec connexion à {device.DisplayName}.");
                return;
            }
            LogError($"Index invalide : {index}.");
            return;
        }

        string cleanAddr = target.Replace(":", "").Replace("-", "").Trim();
        if (ulong.TryParse(cleanAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong addr))
        {
            LogInfo($"Connexion à 0x{addr:X12}...");
            bool ok = await _client!.ConnectAsync(addr);
            if (ok) { LogSuccess("Connecté !"); _session!.StartWatchdog(5); }
            else LogError("Échec connexion.");
            return;
        }

        var byName = _client!.DiscoveredDevices.FirstOrDefault(d =>
            d.Name.Contains(target, StringComparison.OrdinalIgnoreCase) ||
            d.DisplayName.Contains(target, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
        {
            LogInfo($"Connexion à [{byName.DisplayName}]...");
            bool ok = await _client.ConnectAsync(byName);
            if (ok) { LogSuccess($"Connecté à {byName.DisplayName} !"); _session!.StartWatchdog(5); }
            else LogError($"Échec connexion à {byName.DisplayName}.");
            return;
        }

        LogError($"Impossible de résoudre '{target}'. Utilisez 'scan' d'abord.");
    }

    // ── Prompt & Logging ──────────────────────────────────────────────────────

    private static void PrintPrompt()
    {
        lock (ConsoleLock)
        {
            bool connected = _client?.IsConnected == true;
            ConsoleColor c = connected ? ConsoleColor.Green : ConsoleColor.Red;
            string status = connected ? "CONNECTÉ" : "DÉCONNECTÉ";

            Console.ForegroundColor = c;
            Console.Write($"[MONITOR:{status}");
            if (_session != null)
                Console.Write($" | {_session.TimerDisplay}{(_session.IsTimerRunning ? "▶" : "■")} | 🎯{_session.TotalScoresReceived}");
            Console.Write("]");
            Console.ResetColor();
            Console.Write(" > ");
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(@"================================================================================");
        Console.WriteLine(@"       HOUSE OF ARBITRATION — CLI MONITOR (BLUETOOTH LOW ENERGY WINDOWS)       ");
        Console.WriteLine(@"================================================================================");
        Console.ResetColor();
        Console.WriteLine("Moniteur de compétition : reçoit tous les messages, base de données locale, corrections.\n");
    }

    private static void PrintHelpSummary()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Tapez 'scan' pour chercher un serveur, 'help' pour l'aide complète.");
        Console.ResetColor();
    }

    private static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(@"
CONNEXION
  scan [n]                  Scan BLE pendant n secondes (défaut: 5)
  connect <index|adresse>   Connexion au serveur
  disconnect                Déconnexion BLE
  watchdog [n|off]          Ping keep-alive toutes les n secondes (défaut: 5)

ÉTAT
  status (st)               Afficher l'état du monitor
  match (m)                 Demander le match courant
  comp                      Demander la compétition courante
  ping                      Envoyer un ping

BASE DE DONNÉES LOCALE
  history [n]               Historique des n derniers matchs (défaut: 20)
  scores <matchId>          Notes des arbitres pour un match
  export [dossier]          Exporter en CSV
  dump                      Basculer l'affichage en direct des notes (scores)

CORRECTIONS → SERVEUR
  correction winner <matchId> <red|blue> [raison]
                            Corriger le vainqueur d'un match
  correction score <matchId> <judgePos> <red|blue> <score> [raison]
                            Corriger un score d'arbitre

DIVERS
  raw <message>             Envoyer un message brut BLE
  clear (cls)               Vider la console
  help (?)                  Cet écran
  quit (q)                  Quitter

ARGUMENTS DE DÉMARRAGE
  -s / --scan               Scan automatique au démarrage
  -c / --connect <cible>    Connexion automatique au démarrage
  --db <chemin>             Chemin de la base SQLite (défaut: monitor_<date>.db)
  --no-db                   Base en mémoire uniquement (non persistée)
  --dump-scores             Afficher chaque note dès réception");
        Console.ResetColor();
        Console.WriteLine();
    }

    // ── Helpers console ───────────────────────────────────────────────────────

    private static void LogInfo(string msg)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            Console.ResetColor();
        }
    }

    private static void LogError(string msg)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ {msg}");
            Console.ResetColor();
        }
    }

    private static void LogWarning(string msg)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠ {msg}");
            Console.ResetColor();
        }
    }

    private static void LogSuccess(string msg)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {msg}");
            Console.ResetColor();
        }
    }

    private static void LogColor(ConsoleColor color, string msg)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            Console.ResetColor();
        }
    }
}
