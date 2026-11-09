using System.Text;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Server.Cli;

internal static class Program
{
    private static readonly object ConsoleLock = new();
    private static WindowsBluetoothServer? _server;
    private static ArbitrationServerSession? _session;
    private static bool _running = true;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintBanner();

        _server = new WindowsBluetoothServer();
        _session = new ArbitrationServerSession(_server);

        HookEvents(_server, _session);

        bool autoStart = false;
        string deviceName = "HouseOfArbitration-Server";
        CompetitionType initialMode = CompetitionType.Sanda;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            if (arg is "-h" or "--help" or "/?")
            {
                PrintHelp();
                return 0;
            }
            if (arg is "-a" or "--auto-start" or "--start")
            {
                autoStart = true;
            }
            else if (arg is "-n" or "--name" && i + 1 < args.Length)
            {
                deviceName = args[++i];
            }
            else if (arg is "-m" or "--mode" && i + 1 < args.Length)
            {
                string m = args[++i].ToLowerInvariant();
                if (m.StartsWith("t")) initialMode = CompetitionType.Taolu;
                else initialMode = CompetitionType.Sanda;
            }
        }

        if (initialMode != CompetitionType.Sanda)
        {
            await _session.SetCompetitionTypeAsync(initialMode);
        }

        if (autoStart)
        {
            await _server.StartAdvertisingAsync(deviceName);
        }

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

        // Nettoyage avant fermeture
        if (_server?.IsAdvertising == true)
        {
            await _server.SendToAllAsync(Constants.Message.SERVER_STOPPED);
            await Task.Delay(250);
            _server.StopAdvertising();
        }
        _session?.Dispose();

        LogInfo("Serveur CLI arrêté.");
        return 0;
    }

    private static void HookEvents(WindowsBluetoothServer server, ArbitrationServerSession session)
    {
        server.LogMessage += (_, msg) => LogInfo($"[BLE] {msg}");

        session.StatusChanged += (_, msg) => LogInfo(msg);

        session.JudgeConnected += (_, judge) =>
        {
            LogSuccess($"[CONNEXION JUGE] {judge.Name} (Groupe: {judge.Group ?? "Sanda"}, #{judge.Number}) est maintenant CONNECTÉ !");
        };

        session.JudgeDisconnected += (_, judge) =>
        {
            LogWarning($"[DÉCONNEXION JUGE] {judge.Name} s'est déconnecté.");
        };

        session.ScoreUpdated += (_, e) =>
        {
            LogColor(ConsoleColor.Magenta, $"[ACTION NOTATION] {e.Judge.Name} : {e.Details}");
        };

        session.TimerTick += (_, display) =>
        {
            // Affichage discret si le chrono tourne
            // Évite de polluer la console en mode plein texte
        };
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"================================================================================");
        Console.WriteLine(@"       HOUSE OF ARBITRATION - SERVEUR CLI (BLUETOOTH LOW ENERGY WINDOWS)       ");
        Console.WriteLine(@"================================================================================");
        Console.ResetColor();
        Console.WriteLine("Serveur central d'arbitrage diffusant le service GATT BLE pour les juges.\n");
    }

    private static void PrintHelpSummary()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Commandes rapides : 'start' pour démarrer la diffusion BLE, 'status', 'timer start', 'help' pour la liste.");
        Console.ResetColor();
    }

    private static void PrintPrompt()
    {
        lock (ConsoleLock)
        {
            bool isAdv = _server?.IsAdvertising == true;
            string advStatus = isAdv ? "ACTIF" : "ARRÊTÉ";
            ConsoleColor advColor = isAdv ? ConsoleColor.Green : ConsoleColor.Red;

            int connectedCount = _session?.Judges.Count(j => j.IsConnected) ?? 0;
            int totalJudges = _session?.Judges.Count ?? 0;

            Console.ForegroundColor = advColor;
            Console.Write($"[SERVEUR: {advStatus}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($" | {_session?.CurrentCompetitionType}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($" | {connectedCount}/{totalJudges} Juges");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($" | {_session?.TimeLeftDisplay}");
            Console.ResetColor();
            Console.Write("] > ");
        }
    }

    private static async Task HandleCommandAsync(string commandLine)
    {
        string[] parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string cmd = parts[0].ToLowerInvariant();
        string[] args = parts.Skip(1).ToArray();

        try
        {
            switch (cmd)
            {
                case "start":
                    string devName = args.Length > 0 ? string.Join(" ", args) : _server!.DeviceName;
                    await _server!.StartAdvertisingAsync(devName);
                    break;

                case "stop":
                    if (_server!.IsAdvertising)
                    {
                        await _server.SendToAllAsync(Constants.Message.SERVER_STOPPED);
                        await Task.Delay(250);
                        _server.StopAdvertising();
                        _session?.ResetJudgesConnection();
                        LogInfo("Serveur arrêté et message d'arrêt transmis aux juges.");
                    }
                    else
                    {
                        LogWarning("Le serveur BLE n'est pas actif.");
                    }
                    break;

                case "mode":
                    if (args.Length == 0)
                    {
                        LogError("Usage: mode <sanda | taolu>");
                        return;
                    }
                    string modeStr = args[0].ToLowerInvariant();
                    var newType = modeStr.StartsWith("t") ? CompetitionType.Taolu : CompetitionType.Sanda;
                    await _session!.SetCompetitionTypeAsync(newType);
                    break;

                case "timer":
                case "t":
                    await HandleTimerCommandAsync(args);
                    break;

                case "match":
                case "m":
                    await HandleMatchCommandAsync(args);
                    break;

                case "judges":
                case "j":
                    PrintJudges();
                    break;

                case "reset":
                    _session!.ResetMatchScores();
                    break;

                case "broadcast":
                case "b":
                    if (args.Length == 0)
                    {
                        LogError("Usage: broadcast <message>");
                        return;
                    }
                    string bMsg = string.Join(" ", args);
                    await _server!.SendToAllAsync(bMsg);
                    LogInfo($"Message diffusé à tous : '{bMsg}'");
                    break;

                case "send":
                    if (args.Length < 2)
                    {
                        LogError("Usage: send <clientId> <message>");
                        return;
                    }
                    string targetClient = args[0];
                    string cMsg = string.Join(" ", args.Skip(1));
                    await _server!.SendToClientAsync(targetClient, cMsg);
                    LogInfo($"Message envoyé à [{targetClient}] : '{cMsg}'");
                    break;

                case "status":
                    PrintStatus();
                    break;

                case "clear":
                case "cls":
                    Console.Clear();
                    PrintBanner();
                    break;

                case "help":
                case "?":
                    PrintHelp();
                    break;

                case "quit":
                case "exit":
                case "q":
                    _running = false;
                    break;

                default:
                    LogError($"Commande inconnue : '{cmd}'. Tapez 'help' pour afficher la liste des commandes.");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError($"Erreur lors de l'exécution de la commande : {ex.Message}");
        }
    }

    private static async Task HandleTimerCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            LogError("Usage: timer <start | pause | stop | set mm:ss>");
            return;
        }

        string sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "start":
            case "s":
                await _session!.StartTimerAsync();
                break;

            case "pause":
            case "p":
                await _session!.PauseTimerAsync();
                break;

            case "stop":
                await _session!.StopTimerAsync();
                break;

            case "set":
                if (args.Length < 2)
                {
                    LogError("Usage: timer set mm:ss (ex: timer set 02:00 ou timer set 90)");
                    return;
                }
                TimeSpan duration;
                if (args[1].Contains(':'))
                {
                    var segs = args[1].Split(':');
                    if (segs.Length == 2 && int.TryParse(segs[0], out int min) && int.TryParse(segs[1], out int sec))
                    {
                        duration = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
                    }
                    else
                    {
                        LogError("Format de temps invalide (attendu: mm:ss).");
                        return;
                    }
                }
                else if (int.TryParse(args[1], out int totalSec))
                {
                    duration = TimeSpan.FromSeconds(totalSec);
                }
                else
                {
                    LogError("Format de durée non reconnu.");
                    return;
                }
                await _session!.SetTimerDurationAsync(duration);
                break;

            default:
                LogError($"Sous-commande timer inconnue : '{sub}'. Options: start, pause, stop, set mm:ss.");
                break;
        }
    }

    private static async Task HandleMatchCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintCurrentMatch();
            return;
        }

        string sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "next":
            case "n":
                await _session!.NextMatchAsync();
                break;

            case "prev":
            case "p":
                await _session!.PreviousMatchAsync();
                break;

            case "set":
                if (args.Length < 2 || !int.TryParse(args[1], out int id))
                {
                    LogError("Usage: match set <index_ou_id>");
                    return;
                }
                await _session!.SetMatchIndexAsync(id - 1);
                break;

            case "list":
            case "l":
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n--- LISTE DES MATCHS ---");
                Console.ResetColor();
                for (int i = 0; i < _session!.Matches.Count; i++)
                {
                    var m = _session.Matches[i];
                    string active = (_session.CurrentMatch?.Id == m.Id) ? " [*ACTIF*]" : "";
                    Console.WriteLine($"  [{i + 1}] ID #{m.Id} | Catégorie: {m.Category} | {m.RedCompetitor} vs {m.BlueCompetitor}{active}");
                }
                Console.WriteLine();
                break;

            default:
                PrintCurrentMatch();
                break;
        }
    }

    private static void PrintCurrentMatch()
    {
        var m = _session!.CurrentMatch;
        if (m == null)
        {
            LogInfo("Aucun match configuré.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n--- MATCH ACTIF #{m.Id} ---");
        Console.ResetColor();
        Console.WriteLine($"  Catégorie        : {m.Category}");
        Console.WriteLine($"  Type             : {m.Type}");
        Console.WriteLine($"  Coin ROUGE       : {m.RedCompetitor}");
        Console.WriteLine($"  Coin BLEU        : {m.BlueCompetitor}");
        Console.WriteLine();
    }

    private static void PrintJudges()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- ÉTAT DES JUGES ({_session!.CurrentCompetitionType}) ---");
        Console.ResetColor();

        if (_session.CurrentCompetitionType == CompetitionType.Sanda)
        {
            Console.WriteLine(string.Format("  {0,-10} | {1,-12} | {2,10} | {3,10}", "Juge", "Statut", "Pts Rouge", "Pts Bleu"));
            Console.WriteLine("  " + new string('-', 52));
            foreach (var j in _session.Judges)
            {
                string status = j.IsConnected ? "CONNECTÉ" : "DÉCONNECTÉ";
                ConsoleColor col = j.IsConnected ? ConsoleColor.Green : ConsoleColor.DarkGray;

                Console.ForegroundColor = col;
                Console.WriteLine(string.Format("  {0,-10} | {1,-12} | {2,10:F0} | {3,10:F0}", j.Name, status, j.RedPoints, j.BluePoints));
                Console.ResetColor();
            }

            int totalRed = (int)_session.Judges.Sum(j => j.RedPoints);
            int totalBlue = (int)_session.Judges.Sum(j => j.BluePoints);
            Console.WriteLine("  " + new string('-', 52));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  TOTAL POINTS  : Rouge = {totalRed}  |  Bleu = {totalBlue}\n");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(string.Format("  {0,-12} | {1,-8} | {2,-12} | {3,8} | {4,10}", "Juge", "Groupe", "Statut", "Score", "Pénalités"));
            Console.WriteLine("  " + new string('-', 62));
            foreach (var j in _session.Judges)
            {
                string status = j.IsConnected ? "CONNECTÉ" : "DÉCONNECTÉ";
                ConsoleColor col = j.IsConnected ? ConsoleColor.Green : ConsoleColor.DarkGray;

                Console.ForegroundColor = col;
                Console.WriteLine(string.Format("  {0,-12} | {1,-8} | {2,-12} | {3,8:F2} | {4,10}", j.Name, $"Grp {j.Group}", status, j.Score, j.Codes.Count));
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }

    private static void PrintStatus()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n--- ÉTAT GLOBAL DU SERVEUR D'ARBITRAGE ---");
        Console.ResetColor();

        Console.WriteLine($"  Diffusion BLE    : {(_server!.IsAdvertising ? "ACTIVE" : "ARRÊTÉE")}");
        Console.WriteLine($"  Nom local BLE    : {_server.DeviceName}");
        Console.WriteLine($"  Service UUID     : {WindowsBluetoothServer.ArbitrationServiceUuid}");
        Console.WriteLine($"  Mode compétition : {_session!.CurrentCompetitionType}");
        Console.WriteLine($"  Chronomètre      : {_session.TimeLeftDisplay} {(_session.IsTimerRunning ? "[EN COURS]" : "[ARRÊTÉ]")}");
        Console.WriteLine($"  Clients abonnés  : {_server.ConnectedClientIds.Count}");

        if (_session.CurrentMatch != null)
        {
            Console.WriteLine($"  Match courant    : #{_session.CurrentMatch.Id} - {_session.CurrentMatch.Category} ({_session.CurrentMatch.RedCompetitor} vs {_session.CurrentMatch.BlueCompetitor})");
        }

        PrintJudges();
    }

    private static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n--- COMMANDES DISPONIBLES (SERVEUR CLI) ---");
        Console.ResetColor();
        Console.WriteLine("  start [nom]            : Démarre le serveur GATT et diffuse le service BLE.");
        Console.WriteLine("  stop                   : Arrête la diffusion BLE et notifie les juges.");
        Console.WriteLine("  mode <sanda|taolu>     : Définit le mode de compétition (Sanda 5 juges / Taolu A,B,C).");
        Console.WriteLine("  timer start, t s       : Démarre le compte à rebours.");
        Console.WriteLine("  timer pause, t p       : Met en pause le chronomètre.");
        Console.WriteLine("  timer stop             : Arrête et remet le chronomètre à zéro.");
        Console.WriteLine("  timer set <mm:ss>      : Définit la durée du chrono (ex: timer set 02:00 ou 90).");
        Console.WriteLine("  match next, m n        : Passe au match suivant et diffuse les infos aux juges.");
        Console.WriteLine("  match prev, m p        : Revient au match précédent.");
        Console.WriteLine("  match list             : Liste tous les matchs disponibles.");
        Console.WriteLine("  match set <#id>        : Active un match spécifique.");
        Console.WriteLine("  judges, j              : Affiche l'état détaillé de tous les juges et leurs scores.");
        Console.WriteLine("  reset                  : Réinitialise les scores du match actif.");
        Console.WriteLine("  broadcast <message>    : Diffuse une chaîne brute à tous les juges connectés.");
        Console.WriteLine("  send <clientId> <msg>  : Envoie un message brut à un client spécifique.");
        Console.WriteLine("  status                 : Affiche le statut complet du serveur.");
        Console.WriteLine("  clear, cls             : Efface l'écran.");
        Console.WriteLine("  quit, exit             : Arrête le serveur et quitte la CLI.");
        Console.WriteLine();
    }

    private static void LogInfo(string message)
    {
        LogColor(ConsoleColor.White, $"[INFO] {message}");
    }

    private static void LogSuccess(string message)
    {
        LogColor(ConsoleColor.Green, $"[SUCCÈS] {message}");
    }

    private static void LogWarning(string message)
    {
        LogColor(ConsoleColor.DarkYellow, $"[ATTENTION] {message}");
    }

    private static void LogError(string message)
    {
        LogColor(ConsoleColor.Red, $"[ERREUR] {message}");
    }

    private static void LogColor(ConsoleColor color, string message)
    {
        lock (ConsoleLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
