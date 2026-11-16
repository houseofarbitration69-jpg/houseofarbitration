using System.Text;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Server.Cli;

internal static class Program
{
    private static readonly object ConsoleLock = new();
    private static WindowsBluetoothServer? _server;
    private static ArbitrationServerSession? _session;
    private static bool _running = true;
    private static string _deviceName = "HouseOfArbitration-Server";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintBanner();

        _server = new WindowsBluetoothServer();
        _session = new ArbitrationServerSession(_server);

        HookEvents(_server, _session);

        bool autoStart = false;
        CompetitionType initialMode = CompetitionType.Sanda;
        string? initialExcelFile = null;

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
                _deviceName = args[++i];
            }
            else if (arg is "-m" or "--mode" && i + 1 < args.Length)
            {
                string m = args[++i].ToLowerInvariant();
                if (m.StartsWith("t")) initialMode = CompetitionType.Taolu;
                else initialMode = CompetitionType.Sanda;
            }
            else if (arg is "-e" or "--excel" or "-f" or "--file" && i + 1 < args.Length)
            {
                initialExcelFile = args[++i];
            }
        }

        if (!string.IsNullOrWhiteSpace(initialExcelFile))
        {
            await LoadExcelFileAsync(initialExcelFile);
        }
        else if (initialMode != CompetitionType.Sanda)
        {
            await _session.SetCompetitionTypeAsync(initialMode);
        }

        if (autoStart)
        {
            await _server.StartAdvertisingAsync(_deviceName);
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
            string roundBadge = session.CurrentMatch != null ? $"[R{session.CurrentMatch.CurrentRound}] " : "";
            LogColor(ConsoleColor.Magenta, $"[ACTION NOTATION] {roundBadge}{e.Judge.Name} : {e.Details}");
        };

        session.WinnerDeclared += (_, e) =>
        {
            LogColor(ConsoleColor.Yellow, $"🏆 [VAINQUEUR DÉCLARÉ] Match #{e.Match.Id} : {e.Winner} ({e.Reason})");
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
        Console.WriteLine("Commandes rapides : 'start', 'load <fichier.xlsx>', 'round next', 'winner', 'export results', 'export scores', 'history', 'scores history', 'help'.");
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

            string compName = _session?.CurrentCompetition?.Name ?? "Wushu";
            if (compName.Length > 16) compName = compName.Substring(0, 13) + "...";

            var curMatch = _session?.CurrentMatch;
            string matchInfo = curMatch != null
                ? (curMatch.Type != RoundType.Order ? $" | M#{curMatch.Id} R{curMatch.CurrentRound}/{curMatch.MaxRounds}" : $" | M#{curMatch.Id}")
                : "";

            Console.ForegroundColor = advColor;
            Console.Write($"[SERVEUR: {advStatus}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($" | {compName} ({_session?.CurrentCompetitionType}){matchInfo}");
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
                    string devName = args.Length > 0 ? string.Join(" ", args) : (!string.IsNullOrWhiteSpace(_deviceName) ? _deviceName : _server!.DeviceName);
                    _deviceName = devName;
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

                case "load":
                case "excel":
                case "import":
                    if (args.Length == 0)
                    {
                        LogError("Usage: load <fichier.xlsx> (ex: load competition.xlsx)");
                        return;
                    }
                    string filePath = string.Join(" ", args).Trim('\"', '\'');
                    await LoadExcelFileAsync(filePath);
                    break;

                case "export":
                case "exp":
                    HandleExportCommand(args);
                    break;

                case "template":
                case "export-template":
                    string tplPath = args.Length > 0 ? string.Join(" ", args).Trim('\"', '\'') : "competition_template.xlsx";
                    ExportExcelTemplate(tplPath);
                    break;

                case "timer":
                case "t":
                    await HandleTimerCommandAsync(args);
                    break;

                case "match":
                case "m":
                    await HandleMatchCommandAsync(args);
                    break;

                case "round":
                case "r":
                case "rd":
                    await HandleRoundCommandAsync(args);
                    break;

                case "winner":
                case "win":
                case "w":
                case "vainqueur":
                    await HandleWinnerCommandAsync(args);
                    break;

                case "category":
                case "cat":
                case "c":
                    await HandleCategoryCommandAsync(args);
                    break;

                case "history":
                case "hist":
                case "h":
                case "historique":
                    await HandleHistoryCommandAsync(args);
                    break;

                case "scores":
                case "score":
                case "notes":
                case "note":
                    await HandleScoresCommandAsync(args);
                    break;

                case "judges":
                case "j":
                    if (args.Length > 0 && args[0].ToLowerInvariant() is "history" or "hist" or "h" or "notes")
                    {
                        await HandleScoresCommandAsync(args.Skip(1).ToArray());
                    }
                    else
                    {
                        PrintJudges();
                    }
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

            case "winner":
            case "win":
            case "w":
                await HandleWinnerCommandAsync(args.Skip(1).ToArray());
                break;

            case "round":
            case "r":
            case "next-round":
            case "nr":
                await HandleRoundCommandAsync(args.Skip(1).ToArray());
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
                    string winner = m.IsFinished && !string.IsNullOrEmpty(m.Winner)
                        ? $" [🏆 {m.Winner}]"
                        : "";
                    string stage = !string.IsNullOrWhiteSpace(m.StageName) ? $" ({m.StageName})" : "";
                    Console.WriteLine($"  [{i + 1}] ID #{m.Id} | Catégorie: {m.Category}{stage} | {m.RedCompetitor} vs {m.BlueCompetitor}{active}{winner}");
                }
                Console.WriteLine();
                break;

            default:
                PrintCurrentMatch();
                break;
        }
    }

    private static async Task HandleRoundCommandAsync(string[] args)
    {
        var match = _session!.CurrentMatch;
        if (match == null)
        {
            LogError("Aucun match configuré ou actif.");
            return;
        }

        if (args.Length == 0)
        {
            PrintRoundStatus();
            return;
        }

        string sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "next":
            case "n":
                await _session.NextRoundAsync();
                break;

            case "red":
            case "rouge":
            case "r":
                await _session.NextRoundAsync("red");
                break;

            case "blue":
            case "bleu":
            case "b":
                await _session.NextRoundAsync("blue");
                break;

            case "draw":
            case "nul":
            case "d":
            case "egalite":
            case "ex":
                await _session.NextRoundAsync("draw");
                break;

            case "reset":
            case "clear":
                _session.ResetMatchScores();
                LogInfo($"Scores et rounds réinitialisés au Round 1 pour le match #{match.Id}.");
                break;

            default:
                LogError($"Sous-commande round inconnue : '{sub}'. Options: next (ou n), red (ou r), blue (ou b), draw (ou d), reset.");
                break;
        }
    }

    private static void PrintRoundStatus()
    {
        var match = _session!.CurrentMatch;
        if (match == null)
        {
            LogError("Aucun match actif.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- ÉTAT DES ROUNDS DU MATCH #{match.Id} ---");
        Console.ResetColor();

        string stage = !string.IsNullOrWhiteSpace(match.StageName) ? $" ({match.StageName})" : "";
        Console.WriteLine($"  Catégorie        : {match.Category}{stage}");
        Console.WriteLine($"  Combattants      : {match.RedCompetitor} (Rouge) vs {match.BlueCompetitor} (Bleu)");
        Console.WriteLine($"  Round actuel     : Round {match.CurrentRound} / {match.MaxRounds}");

        string timerStatus = _session.IsTimerRunning ? "EN COURS" : "ARRÊTÉ / PAUSE";
        Console.WriteLine($"  Chronomètre      : {_session.TimeLeftDisplay} ({timerStatus})");

        var activeJudges = _session.GetActiveJudges();
        int curRed = (int)activeJudges.Sum(j => j.RedPoints);
        int curBlue = (int)activeJudges.Sum(j => j.BluePoints);
        Console.WriteLine($"  Score Round {match.CurrentRound}  : Rouge {curRed} pts vs {curBlue} pts Bleu");

        if (match.RoundResults.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  Historique des rounds terminés :");
            foreach (var r in match.RoundResults)
            {
                string winnerDesc = r.WinnerColor == "Red" ? $"🏆 {match.RedCompetitor} (Rouge)" : (r.WinnerColor == "Blue" ? $"🏆 {match.BlueCompetitor} (Bleu)" : "⚖️ Match nul");
                Console.WriteLine($"    - Round {r.RoundNumber} : {winnerDesc} | Points juges : {r.RedVotes} Rouge - {r.BlueVotes} Bleu");
            }

            int redWins = match.RoundResults.Count(r => r.WinnerColor == "Red");
            int blueWins = match.RoundResults.Count(r => r.WinnerColor == "Blue");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Total victoires  : Rouge {redWins} - {blueWins} Bleu");
            Console.ResetColor();
        }

        if (match.IsFinished)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  Match TERMINÉ | Vainqueur final : {match.Winner} ({match.DecisionReason})");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n  Commandes pour gérer les rounds :");
            Console.WriteLine("    round next (ou r n, r)    : Valide le round (auto d'après les juges) et passe au suivant");
            Console.WriteLine("    round red  (ou r r)       : Valide le round pour Rouge et passe au suivant");
            Console.WriteLine("    round blue (ou r b)       : Valide le round pour Bleu et passe au suivant");
            Console.WriteLine("    round draw (ou r d)       : Valide le round en match nul et passe au suivant");
            Console.WriteLine("    round reset               : Réinitialise les scores et remet au Round 1");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    private static async Task HandleWinnerCommandAsync(string[] args)
    {
        var match = _session!.CurrentMatch;
        if (match == null)
        {
            LogError("Aucun match configuré ou actif.");
            return;
        }

        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n--- ÉTAT / VAINQUEUR DU MATCH #{match.Id} ---");
            Console.ResetColor();

            if (match.IsFinished && !string.IsNullOrEmpty(match.Winner))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  Match TERMINÉ");
                Console.WriteLine($"  🏆 Vainqueur validé : {match.Winner}");
                if (match.WinnerSide.HasValue) Console.WriteLine($"  Coin                : {match.WinnerSide}");
                if (match.FinalScore > 0) Console.WriteLine($"  Score final         : {match.FinalScore:F2}");
                if (!string.IsNullOrEmpty(match.DecisionReason)) Console.WriteLine($"  Détails/Décision    : {match.DecisionReason}");
                Console.ResetColor();
                Console.WriteLine("\n  Pour modifier : 'winner red', 'winner blue', ou 'winner clear' pour réouvrir.");
                return;
            }

            if (_session.CurrentCompetitionType == CompetitionType.Sanda && match.Type != RoundType.Order)
            {
                var eval = _session.EvaluateSandaScores();
                Console.WriteLine($"  Coin ROUGE : {match.RedCompetitor} ({eval.TotalRedPts} pts - {eval.RedVotes} juges)");
                Console.WriteLine($"  Coin BLEU  : {match.BlueCompetitor} ({eval.TotalBluePts} pts - {eval.BlueVotes} juges)");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  Tendance actuelle : {eval.Leader} ({eval.Summary})");
                Console.ResetColor();
                Console.WriteLine("\n  Commandes pour valider le vainqueur :");
                Console.WriteLine("    winner red   (ou w r)    : Valide le coin ROUGE vainqueur");
                Console.WriteLine("    winner blue  (ou w b)    : Valide le coin BLEU vainqueur");
                Console.WriteLine("    winner auto  (ou w auto) : Valide automatiquement selon les points des juges");
                Console.WriteLine("    winner draw  (ou w draw) : Déclare un match nul");
            }
            else
            {
                var eval = _session.EvaluateTaoluScores();
                Console.WriteLine($"  Athlète : {match.RedCompetitor}");
                Console.WriteLine($"  Note calculée : {eval.TotalScore:F2} (Qualité A: {eval.ScoreA:F2} + Perf B: {eval.ScoreB:F2} + Diff C: {eval.ScoreC:F2})");
                Console.WriteLine("\n  Commandes disponibles :");
                Console.WriteLine("    winner auto             : Valide la note calculée des juges");
                Console.WriteLine("    winner <note> (ex: 9.35): Fixe et valide une note manuelle");
            }
            return;
        }

        string sub = args[0].ToLowerInvariant();
        string? customReason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : null;

        if (sub is "clear" or "reset" or "cancel")
        {
            await _session.ClearWinnerAsync();
            LogSuccess($"Vainqueur annulé pour le match #{match.Id}. Le match est réouvert.");
            return;
        }

        bool success = await _session.SetWinnerAsync(sub, customReason);
        if (success && match.IsFinished)
        {
            LogSuccess($"Vainqueur validé pour le match #{match.Id} : {match.Winner}");
        }
    }

    private static async Task HandleCategoryCommandAsync(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase) || args[0].Equals("l", StringComparison.OrdinalIgnoreCase))
        {
            PrintCategoryList();
            return;
        }

        string sub = args[0].ToLowerInvariant();
        if (sub is "export" or "excel" or "xlsx")
        {
            string? target = args.Length > 1 ? string.Join(" ", args.Skip(1)).Trim('\"', '\'') : null;
            ExportResults(target);
            return;
        }

        if (sub is "history" or "hist" or "h" or "historique")
        {
            string? target = args.Length > 1 ? string.Join(" ", args.Skip(1)) : null;
            PrintCategoryHistory(target);
            return;
        }

        // Si l'utilisateur tape directement: category 1 ou category "Sanda -70kg"
        string catTarget = string.Join(" ", args);
        PrintCategoryHistory(catTarget);
    }

    private static async Task HandleHistoryCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            // Historique du match courant, ou liste si aucun match
            PrintCategoryHistory(null);
            return;
        }

        string sub = args[0].ToLowerInvariant();
        if (sub is "export" or "excel" or "xlsx")
        {
            string? exportTarget = args.Length > 1 ? string.Join(" ", args.Skip(1)).Trim('\"', '\'') : null;
            ExportResults(exportTarget);
            return;
        }

        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase) || args[0].Equals("l", StringComparison.OrdinalIgnoreCase))
        {
            PrintCategoryList();
            return;
        }

        string target = string.Join(" ", args);
        PrintCategoryHistory(target);
    }

    private static void PrintCategoryList()
    {
        var summaries = _session!.GetCategoriesSummary();
        if (summaries.Count == 0)
        {
            LogWarning("Aucune catégorie disponible dans la session.");
            Console.WriteLine("Chargez une compétition via 'load <fichier.xlsx>' pour importer des catégories.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- LISTE DES CATÉGORIES ({summaries.Count}) ---");
        Console.ResetColor();

        Console.WriteLine(string.Format("  {0,-4} | {1,-26} | {2,-11} | {3,-9} | {4,-10} | {5}", "#", "Catégorie", "Type", "Matchs", "Statut", "Vainqueur / En tête"));
        Console.WriteLine("  " + new string('-', 85));

        for (int i = 0; i < summaries.Count; i++)
        {
            var cat = summaries[i];
            ConsoleColor statusColor = cat.IsFinished ? ConsoleColor.Green : (cat.IsInProgress ? ConsoleColor.Yellow : ConsoleColor.DarkGray);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(string.Format("  [{0,2}] | {1,-26} | {2,-11} | {3,3}/{4,-3}   | ", cat.Index, Truncate(cat.Name, 26), cat.RoundType, cat.CompletedMatches, cat.TotalMatches));

            Console.ForegroundColor = statusColor;
            Console.Write(string.Format("{0,-10}", cat.StatusText));
            Console.ResetColor();

            Console.Write(" | ");
            if (!string.IsNullOrWhiteSpace(cat.LeaderOrWinner))
            {
                Console.ForegroundColor = cat.IsFinished ? ConsoleColor.Yellow : ConsoleColor.Cyan;
                string trophy = cat.IsFinished ? "🏆 " : "⭐ ";
                Console.WriteLine(trophy + cat.LeaderOrWinner);
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("--");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Astuce : Tapez 'history <#id ou nom>' (ex: 'history 1' ou 'history Sanda') pour voir l'historique détaillé.");
        Console.WriteLine("           Tapez 'history' seul pour voir la catégorie du match actif.\n");
        Console.ResetColor();
    }

    private static void PrintCategoryHistory(string? categoryNameOrId)
    {
        var report = _session!.GetCategoryHistory(categoryNameOrId);
        if (report == null)
        {
            if (string.IsNullOrWhiteSpace(categoryNameOrId))
            {
                LogError("Aucun match en cours pour déterminer la catégorie. Tapez 'history list' pour afficher les catégories disponibles.");
            }
            else
            {
                LogError($"Catégorie '{categoryNameOrId}' introuvable. Tapez 'history list' pour voir les catégories disponibles.");
            }
            return;
        }

        var sum = report.Summary;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n" + new string('=', 82));
        Console.WriteLine($"  📂 HISTORIQUE DE LA CATÉGORIE : {sum.Name}");
        Console.WriteLine(new string('=', 82));
        Console.ResetColor();

        Console.WriteLine($"  Type de tour   : {sum.RoundType}");
        Console.Write($"  Progression    : {sum.CompletedMatches} / {sum.TotalMatches} match(s) terminé(s) ({sum.ProgressPercentage:F0}%) - Statut : ");

        ConsoleColor statusColor = sum.IsFinished ? ConsoleColor.Green : (sum.IsInProgress ? ConsoleColor.Yellow : ConsoleColor.DarkGray);
        Console.ForegroundColor = statusColor;
        Console.WriteLine(sum.StatusText);
        Console.ResetColor();

        if (!string.IsNullOrWhiteSpace(report.CategoryWinner))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            string prefix = sum.IsFinished ? "🏆 VAINQUEUR DE LA CATÉGORIE" : "⭐ LEADER ACTUEL";
            string reason = !string.IsNullOrWhiteSpace(report.DecisionReason) ? $" ({report.DecisionReason})" : "";
            Console.WriteLine($"  {prefix} : {report.CategoryWinner}{reason}");
            Console.ResetColor();
        }

        // 1. Liste chronologique des matchs
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- HISTORIQUE DES MATCHS & PASSAGES ({report.Matches.Count}) ---");
        Console.ResetColor();

        for (int i = 0; i < report.Matches.Count; i++)
        {
            var m = report.Matches[i];
            bool isCurrent = _session.CurrentMatch?.Id == m.Id;
            string currentFlag = isCurrent ? " [*ACTIF*]" : "";

            Console.ForegroundColor = isCurrent ? ConsoleColor.Cyan : ConsoleColor.White;
            Console.Write($"  [{i + 1}] ID #{m.Id}{currentFlag} : ");
            Console.ResetColor();

            if (m.Type == RoundType.Order)
            {
                Console.WriteLine($"Athlète : {m.RedCompetitor}");
            }
            else
            {
                Console.WriteLine($"🔴 {m.RedCompetitor}  vs  🔵 {m.BlueCompetitor}");
            }

            // Statut et détails du match
            if (m.IsFinished)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("      Statut    : [TERMINÉ] ");
                Console.ResetColor();

                if (m.Type == RoundType.Order)
                {
                    Console.WriteLine($"Note finale : {m.FinalScore:F2} / 10.00");
                    if (m.JudgeScoresByJudge.Count > 0)
                    {
                        string judgeScoresStr = string.Join(" | ", m.JudgeScoresByJudge.Select(kv => $"{kv.Key}: {kv.Value:F2}"));
                        Console.WriteLine($"      Notes juges: {judgeScoresStr}");
                    }
                    else if (m.JudgeScores.Count > 0)
                    {
                        string judgeScoresStr = string.Join(", ", m.JudgeScores.Select(s => s.ToString("F2")));
                        Console.WriteLine($"      Notes juges: {judgeScoresStr}");
                    }
                }
                else
                {
                    Console.WriteLine($"Score : Rouge {m.RedScore} - Bleu {m.BlueScore}");
                }

                if (!string.IsNullOrWhiteSpace(m.Winner))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    string reason = !string.IsNullOrWhiteSpace(m.DecisionReason) ? $" ({m.DecisionReason})" : "";
                    Console.WriteLine($"      Vainqueur : 🏆 {m.Winner}{reason}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = isCurrent ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
                Console.WriteLine(isCurrent ? "      Statut    : [EN COURS D'ARBITRAGE]" : "      Statut    : [À VENIR / NON DÉBUTÉ]");
                Console.ResetColor();
            }

            if (i < report.Matches.Count - 1)
            {
                Console.WriteLine("      " + new string('-', 60));
            }
        }

        // 2. Classement / Bilan
        if (report.Standings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            string titleStandings = sum.IsFinished ? "--- CLASSEMENT FINAL DE LA CATÉGORIE ---" : "--- CLASSEMENT PROVISOIRE / BILAN ---";
            Console.WriteLine($"\n{titleStandings}");
            Console.ResetColor();

            if (sum.RoundType == RoundType.Order)
            {
                // Taolu
                Console.WriteLine(string.Format("  {0,-6} | {1,-24} | {2,-12} | {3,-24} | {4}", "Rang", "Athlète", "Note Finale", "Notes juges", "Statut"));
                Console.WriteLine("  " + new string('-', 84));

                foreach (var row in report.Standings)
                {
                    string medal = row.Rank == 1 ? "🥇 1" : (row.Rank == 2 ? "🥈 2" : (row.Rank == 3 ? "🥉 3" : $"   {row.Rank}"));
                    string scoreStr = row.HasFinished ? $"{row.Score:F2} / 10.00" : "--";
                    string judgeNotes = row.JudgeScoresByJudge.Count > 0
                        ? string.Join(" ", row.JudgeScoresByJudge.Select(kv => $"{kv.Key}:{kv.Value:F2}"))
                        : (row.JudgeScores.Count > 0 ? string.Join(" ", row.JudgeScores.Select(s => s.ToString("F2"))) : "--");

                    ConsoleColor rCol = row.Rank == 1 ? ConsoleColor.Yellow : (row.HasFinished ? ConsoleColor.White : ConsoleColor.DarkGray);

                    Console.ForegroundColor = rCol;
                    Console.WriteLine(string.Format("  {0,-6} | {1,-24} | {2,-12} | {3,-24} | {4}", medal, Truncate(row.CompetitorName, 24), scoreStr, Truncate(judgeNotes, 24), row.Status));
                    Console.ResetColor();
                }
            }
            else
            {
                // Sanda (Knockouts ou Pools)
                Console.WriteLine(string.Format("  {0,-6} | {1,-26} | {2,3} | {3,3} | {4,3} | {5,5} | {6,5} | {7,5} | {8}",
                    "Rang", "Compétiteur", "MJ", "V", "D", "Pts +", "Pts -", "Diff", "Statut"));
                Console.WriteLine("  " + new string('-', 78));

                foreach (var row in report.Standings)
                {
                    string medal = row.Rank == 1 ? "🥇 1" : (row.Rank == 2 ? "🥈 2" : (row.Rank == 3 ? "🥉 3" : $"   {row.Rank}"));
                    ConsoleColor rCol = row.Rank == 1 ? ConsoleColor.Yellow : (row.Rank == 2 ? ConsoleColor.Cyan : ConsoleColor.White);

                    Console.ForegroundColor = rCol;
                    Console.WriteLine(string.Format("  {0,-6} | {1,-26} | {2,3} | {3,3} | {4,3} | {5,5} | {6,5} | {7,5} | {8}",
                        medal,
                        Truncate(row.CompetitorName, 26),
                        row.MatchesPlayed,
                        row.Wins,
                        row.Losses,
                        row.PointsFor,
                        row.PointsAgainst,
                        $"{row.PointDifference:+0;-0;0}",
                        row.Status));
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine("\n" + new string('=', 82) + "\n");
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
    }

    private static void PrintCurrentMatch()
    {
        var m = _session!.CurrentMatch;
        if (m == null)
        {
            if (_session.Matches.Count > 0)
            {
                LogColor(ConsoleColor.Yellow, "🏁 [FIN DES MATCHS] Tous les matchs de la compétition sont terminés !");
            }
            else
            {
                LogInfo("Aucun match configuré.");
            }
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n--- MATCH ACTIF #{m.Id} ---");
        Console.ResetColor();
        string stage = !string.IsNullOrWhiteSpace(m.StageName) ? $" ({m.StageName})" : "";
        Console.WriteLine($"  Catégorie        : {m.Category}{stage}");
        Console.WriteLine($"  Type             : {m.Type}");
        if (m.Type != RoundType.Order)
        {
            Console.WriteLine($"  Round            : Round {m.CurrentRound}/{m.MaxRounds}");
            if (m.RoundResults.Count > 0)
            {
                var summaryList = m.RoundResults.Select(r => $"R{r.RoundNumber} [{r.WinnerColor}: {r.RedVotes}-{r.BlueVotes}]");
                Console.WriteLine($"  Rounds terminés  : {string.Join(" | ", summaryList)}");
            }
        }
        Console.WriteLine($"  Coin ROUGE       : {m.RedCompetitor}");
        if (!string.IsNullOrWhiteSpace(m.BlueCompetitor))
        {
            Console.WriteLine($"  Coin BLEU        : {m.BlueCompetitor}");
        }

        if (m.IsFinished && !string.IsNullOrEmpty(m.Winner))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Statut           : [TERMINÉ]");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  🏆 VAINQUEUR     : {m.Winner}");
            if (m.WinnerSide.HasValue)
            {
                Console.WriteLine($"  Coin vainqueur   : {m.WinnerSide}");
            }
            if (m.FinalScore > 0)
            {
                Console.WriteLine($"  Note finale      : {m.FinalScore:F2}");
            }
            if (!string.IsNullOrEmpty(m.DecisionReason))
            {
                Console.WriteLine($"  Décision         : {m.DecisionReason}");
            }
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"  Statut           : [EN COURS]");
            if (_session.CurrentCompetitionType == CompetitionType.Sanda && m.Type != RoundType.Order)
            {
                var eval = _session.EvaluateSandaScores();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  Tendance actuelle: {eval.Leader} ({eval.Summary})");
                Console.ResetColor();
            }
            else
            {
                var eval = _session.EvaluateTaoluScores();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  Note calculée    : {eval.TotalScore:F2}");
                Console.ResetColor();
            }
        }
        Console.WriteLine();
    }

    private static void PrintJudges()
    {
        string roundInfo = _session?.CurrentMatch != null && _session.CurrentCompetitionType == CompetitionType.Sanda
            ? $" - Round {_session.CurrentMatch.CurrentRound}/{_session.CurrentMatch.MaxRounds}"
            : "";
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- ÉTAT DES JUGES ({_session!.CurrentCompetitionType}{roundInfo}) ---");
        Console.ResetColor();

        var displayJudges = _session.GetActiveJudges();

        if (_session.CurrentCompetitionType == CompetitionType.Sanda)
        {
            Console.WriteLine(string.Format("  {0,-10} | {1,-12} | {2,10} | {3,10}", "Juge", "Statut", "Pts Rouge", "Pts Bleu"));
            Console.WriteLine("  " + new string('-', 52));
            foreach (var j in displayJudges)
            {
                string status = j.IsConnected ? "CONNECTÉ" : (j.HasConnected ? "DÉCONNECTÉ" : "NON CONNECTÉ");
                ConsoleColor col = j.IsConnected ? ConsoleColor.Green : (j.HasConnected ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray);

                Console.ForegroundColor = col;
                Console.WriteLine(string.Format("  {0,-10} | {1,-12} | {2,10:F0} | {3,10:F0}", j.Name, status, j.RedPoints, j.BluePoints));
                Console.ResetColor();
            }

            int totalRed = (int)displayJudges.Sum(j => j.RedPoints);
            int totalBlue = (int)displayJudges.Sum(j => j.BluePoints);
            Console.WriteLine("  " + new string('-', 52));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  TOTAL POINTS  : Rouge = {totalRed}  |  Bleu = {totalBlue}\n");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(string.Format("  {0,-12} | {1,-8} | {2,-12} | {3,8} | {4,10}", "Juge", "Groupe", "Statut", "Score", "Pénalités"));
            Console.WriteLine("  " + new string('-', 62));
            foreach (var j in displayJudges)
            {
                string status = j.IsConnected ? "CONNECTÉ" : (j.HasConnected ? "DÉCONNECTÉ" : "NON CONNECTÉ");
                ConsoleColor col = j.IsConnected ? ConsoleColor.Green : (j.HasConnected ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray);

                Console.ForegroundColor = col;
                Console.WriteLine(string.Format("  {0,-12} | {1,-8} | {2,-12} | {3,8:F2} | {4,10}", j.Name, $"Grp {j.Group}", status, j.Score, j.Codes.Count));
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }

    private static async Task HandleScoresCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintJudges();
            PrintRecentJudgeScores(limit: 10);
            return;
        }

        string sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "export":
            case "excel":
            case "xlsx":
                string? sTarget = args.Length > 1 ? string.Join(" ", args.Skip(1)).Trim('\"', '\'') : null;
                ExportJudgeScores(sTarget);
                break;

            case "history":
            case "hist":
            case "h":
            case "list":
            case "l":
                if (args.Length > 1)
                {
                    await HandleScoresHistoryFilterAsync(args.Skip(1).ToArray());
                }
                else
                {
                    PrintJudgeScoresHistory(matchId: _session!.CurrentMatch?.Id, judgeFilter: null);
                }
                break;

            case "all":
            case "a":
                PrintJudgeScoresHistory(matchId: null, judgeFilter: null);
                break;

            case "match":
            case "m":
                if (args.Length > 1 && int.TryParse(args[1], out int matchId))
                {
                    PrintJudgeScoresHistory(matchId: matchId, judgeFilter: null);
                }
                else
                {
                    PrintJudgeScoresHistory(matchId: _session!.CurrentMatch?.Id, judgeFilter: null);
                }
                break;

            case "judge":
            case "j":
                if (args.Length > 1)
                {
                    string jTarget = string.Join(" ", args.Skip(1));
                    PrintJudgeScoresHistory(matchId: null, judgeFilter: jTarget);
                }
                else
                {
                    LogError("Usage: scores judge <#numéro ou nom> (ex: scores judge 1 ou scores judge A2)");
                }
                break;

            case "clear":
            case "reset":
                _session!.ClearJudgeScoresHistory();
                LogSuccess("Historique des notes des juges effacé.");
                break;

            default:
                if (int.TryParse(sub, out int directId))
                {
                    PrintJudgeScoresHistory(matchId: directId, judgeFilter: null);
                }
                else
                {
                    PrintJudgeScoresHistory(matchId: null, judgeFilter: sub);
                }
                break;
        }
    }

    private static async Task HandleScoresHistoryFilterAsync(string[] filterArgs)
    {
        string first = filterArgs[0].ToLowerInvariant();
        if (first is "match" or "m" && filterArgs.Length > 1 && int.TryParse(filterArgs[1], out int mId))
        {
            PrintJudgeScoresHistory(matchId: mId, judgeFilter: null);
        }
        else if (first is "judge" or "j" && filterArgs.Length > 1)
        {
            PrintJudgeScoresHistory(matchId: null, judgeFilter: string.Join(" ", filterArgs.Skip(1)));
        }
        else if (int.TryParse(first, out int directMatchId))
        {
            PrintJudgeScoresHistory(matchId: directMatchId, judgeFilter: null);
        }
        else
        {
            PrintJudgeScoresHistory(matchId: null, judgeFilter: string.Join(" ", filterArgs));
        }
    }

    private static void PrintRecentJudgeScores(int limit = 10)
    {
        var entries = _session!.JudgeScoreHistory
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .Reverse()
            .ToList();

        if (entries.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  (Aucune action de notation enregistrée pour l'instant)");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- DERNIÈRES NOTATIONS REÇUES ({entries.Count}) ---");
        Console.ResetColor();

        foreach (var e in entries)
        {
            ConsoleColor col = e.Side == Constants.Side.Red
                ? ConsoleColor.Red
                : (e.Side == Constants.Side.Blue ? ConsoleColor.Cyan : (e.ActionType == "Déduction" ? ConsoleColor.DarkYellow : ConsoleColor.Magenta));

            string matchBadge = e.MatchId > 0 ? $"[M#{e.MatchId}]" : "";
            string roundBadge = e.Round > 0 ? $"[R{e.Round}]" : "";
            string timeBadge = !string.IsNullOrEmpty(e.MatchTimeDisplay) ? $"({e.MatchTimeDisplay})" : "";

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  {e.Timestamp:HH:mm:ss} {timeBadge,-7} {matchBadge,-6} {roundBadge,-5} ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{e.JudgeName,-9} : ");

            Console.ForegroundColor = col;
            Console.WriteLine($"{e.Description}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Tapez 'scores history' pour voir l'historique complet, ou 'scores match <#id>' / 'scores judge <nom>'.\n");
        Console.ResetColor();
    }

    private static void PrintJudgeScoresHistory(int? matchId, string? judgeFilter)
    {
        var entries = _session!.GetJudgeScoresHistory(matchId, judgeFilter);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n" + new string('=', 92));
        Console.WriteLine("  📋 HISTORIQUE DES NOTATIONS DES JUGES");
        Console.WriteLine(new string('=', 92));
        Console.ResetColor();

        if (matchId.HasValue && matchId.Value > 0)
        {
            var match = _session.Matches.FirstOrDefault(m => m.Id == matchId.Value);
            string matchTitle = match != null
                ? $"#{match.Id} - {match.Category} ({match.RedCompetitor} vs {match.BlueCompetitor})"
                : $"#{matchId.Value}";
            Console.WriteLine($"  Filtre Match : {matchTitle}");
        }
        else
        {
            Console.WriteLine("  Filtre Match : TOUS LES MATCHS");
        }

        if (!string.IsNullOrWhiteSpace(judgeFilter))
        {
            Console.WriteLine($"  Filtre Juge  : {judgeFilter}");
        }

        Console.WriteLine($"  Total notat. : {entries.Count} action(s) enregistrée(s)\n");

        if (entries.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Aucune notation trouvée correspondant aux critères.");
            Console.ResetColor();
            Console.WriteLine(new string('=', 92) + "\n");
            return;
        }

        Console.WriteLine(string.Format("  {0,-4} | {1,-8} | {2,-6} | {3,-5} | {4,-5} | {5,-10} | {6,-12} | {7,-22} | {8}",
            "#", "Heure", "Chrono", "Match", "Round", "Juge", "Type", "Détail", "Total"));
        Console.WriteLine("  " + new string('-', 92));

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            ConsoleColor typeColor = e.Side == Constants.Side.Red
                ? ConsoleColor.Red
                : (e.Side == Constants.Side.Blue ? ConsoleColor.Cyan : (e.ActionType == "Déduction" ? ConsoleColor.Yellow : ConsoleColor.White));

            string chrono = !string.IsNullOrEmpty(e.MatchTimeDisplay) ? e.MatchTimeDisplay : "--:--";
            string matchBadge = e.MatchId > 0 ? $"#{e.MatchId}" : "--";
            string roundBadge = e.Round > 0 ? $"R{e.Round}" : "--";

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(string.Format("  [{0,2}] | {1,-8} | {2,-6} | {3,-5} | {4,-5} | {5,-10} | ",
                i + 1, e.Timestamp.ToString("HH:mm:ss"), chrono, matchBadge, roundBadge, e.JudgeName));

            Console.ForegroundColor = typeColor;
            Console.Write(string.Format("{0,-12}", Truncate(e.ActionType, 12)));
            Console.ResetColor();

            Console.Write(string.Format(" | {0,-22} | ", Truncate(e.Description, 22)));

            Console.ForegroundColor = ConsoleColor.Yellow;
            string totalStr = e.CompetitionType == CompetitionType.Sanda
                ? (e.Side == Constants.Side.Red ? $"R:{e.ResultingScore:F0}" : (e.Side == Constants.Side.Blue ? $"B:{e.ResultingScore:F0}" : $"{e.ResultingScore:F0}"))
                : $"{e.ResultingScore:F2}";
            Console.WriteLine(totalStr);
            Console.ResetColor();
        }

        var summary = _session.GetJudgeScoreSummary(matchId);
        Console.WriteLine("  " + new string('-', 92));
        Console.ForegroundColor = ConsoleColor.Cyan;
        if (summary.DeductionEntries > 0)
        {
            Console.WriteLine($"  SYNTHÈSE : {summary.TotalEntries} notation(s) au total | Déductions: {summary.DeductionEntries}");
        }
        else
        {
            Console.WriteLine($"  SYNTHÈSE : {summary.TotalEntries} notation(s) au total | Points Rouge: {summary.RedEntries} | Points Bleu: {summary.BlueEntries}");
        }
        Console.ResetColor();
        Console.WriteLine(new string('=', 92) + "\n");
    }

    private static void PrintStatus()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n--- ÉTAT GLOBAL DU SERVEUR D'ARBITRAGE ---");
        Console.ResetColor();

        Console.WriteLine($"  Diffusion BLE    : {(_server!.IsAdvertising ? "ACTIVE" : "ARRÊTÉE")}");
        Console.WriteLine($"  Nom local BLE    : {_server.DeviceName}");
        Console.WriteLine($"  Service UUID     : {WindowsBluetoothServer.ArbitrationServiceUuid}");
        if (_session?.CurrentCompetition != null)
        {
            Console.WriteLine($"  Compétition      : {_session.CurrentCompetition.Name} ({_session.CurrentCompetition.Date:dd/MM/yyyy})");
        }
        Console.WriteLine($"  Mode compétition : {_session!.CurrentCompetitionType}");
        Console.WriteLine($"  Total matchs     : {_session.Matches.Count}");
        Console.WriteLine($"  Chronomètre      : {_session.TimeLeftDisplay} {(_session.IsTimerRunning ? "[EN COURS]" : "[ARRÊTÉ]")}");
        Console.WriteLine($"  Clients abonnés  : {_server.ConnectedClientIds.Count}");

        if (_session.CurrentMatch != null)
        {
            string roundSuffix = _session.CurrentCompetitionType == CompetitionType.Sanda
                ? $" [Round {_session.CurrentMatch.CurrentRound}/{_session.CurrentMatch.MaxRounds}]"
                : "";
            Console.WriteLine($"  Match courant    : #{_session.CurrentMatch.Id} - {_session.CurrentMatch.Category} ({_session.CurrentMatch.RedCompetitor} vs {_session.CurrentMatch.BlueCompetitor}){roundSuffix}");
        }
        else if (_session.Matches.Count > 0)
        {
            Console.WriteLine("  Match courant    : 🏁 [FIN DES MATCHS] Tous les matchs de la compétition sont terminés !");
        }
        else
        {
            Console.WriteLine("  Match courant    : Aucun match configuré");
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
        Console.WriteLine("  load <fichier.xlsx>    : Charge une compétition, ses catégories et compétiteurs depuis Excel.");
        Console.WriteLine("  template [fichier]     : Génère un modèle de fichier Excel pré-rempli pour organiser la compétition.");
        Console.WriteLine("  export results [fic]   : Exporte les résultats et classements de chaque catégorie dans un fichier Excel.");
        Console.WriteLine("  export scores [fic]    : Exporte l'historique complet des notes des juges (logs) dans un fichier Excel.");
        Console.WriteLine("  export all             : Exporte à la fois les résultats et les notes des juges.");
        Console.WriteLine("  timer start, t s       : Démarre le compte à rebours.");
        Console.WriteLine("  timer pause, t p       : Met en pause le chronomètre.");
        Console.WriteLine("  timer stop             : Arrête et remet le chronomètre à zéro.");
        Console.WriteLine("  timer set <mm:ss>      : Définit la durée du chrono (ex: timer set 02:00 ou 90).");
        Console.WriteLine("  match next, m n        : Passe au match suivant et diffuse les infos aux juges.");
        Console.WriteLine("  match prev, m p        : Revient au match précédent.");
        Console.WriteLine("  match list             : Liste tous les matchs disponibles.");
        Console.WriteLine("  match set <#id>        : Active un match spécifique.");
        Console.WriteLine("  winner <red|blue|auto> : Déclare le vainqueur du match (alias: w r, w b, w auto).");
        Console.WriteLine("  winner clear           : Annule le vainqueur et réouvre le match actif.");
        Console.WriteLine("  winner                 : Affiche l'état des scores actuels et qui mène.");
        Console.WriteLine("  round next, r n, r     : Valide le round actuel et passe au round suivant (calcul auto).");
        Console.WriteLine("  round red, r r         : Valide le round pour le coin ROUGE et passe au round suivant.");
        Console.WriteLine("  round blue, r b        : Valide le round pour le coin BLEU et passe au round suivant.");
        Console.WriteLine("  round draw, r d        : Valide le round en match nul / égalité et passe au round suivant.");
        Console.WriteLine("  round                  : Affiche l'état des rounds du match actif (scores, victoires).");
        Console.WriteLine("  round reset            : Réinitialise les scores et remet le match au Round 1.");
        Console.WriteLine("  history [cat|#id]      : Affiche l'historique complet d'une catégorie (alias: hist, h).");
        Console.WriteLine("  history list           : Liste toutes les catégories avec avancement et vainqueurs.");
        Console.WriteLine("  category list          : Liste synthétique des catégories (alias: cat l).");
        Console.WriteLine("  category history [cat] : Affiche l'historique et le classement d'une catégorie.");
        Console.WriteLine("  judges, j              : Affiche l'état détaillé de tous les juges et leurs scores.");
        Console.WriteLine("  judges history, j h    : Affiche l'historique chronologique des notations.");
        Console.WriteLine("  scores                 : Affiche l'état actuel et les 10 dernières notations reçues.");
        Console.WriteLine("  scores history         : Affiche l'historique complet des notes pour le match actif.");
        Console.WriteLine("  scores match <#id>     : Historique des notes pour un match spécifique.");
        Console.WriteLine("  scores judge <nom|#num>: Historique des notes attribuées par un juge donné.");
        Console.WriteLine("  scores all             : Historique de toutes les notes reçues durant la session.");
        Console.WriteLine("  scores clear           : Efface l'historique des notes.");
        Console.WriteLine("  reset                  : Réinitialise les scores du match actif.");
        Console.WriteLine("  broadcast <message>    : Diffuse une chaîne brute à tous les juges connectés.");
        Console.WriteLine("  send <clientId> <msg>  : Envoie un message brut à un client spécifique.");
        Console.WriteLine("  status                 : Affiche le statut complet du serveur.");
        Console.WriteLine("  clear, cls             : Efface l'écran.");
        Console.WriteLine("  quit, exit             : Arrête le serveur et quitte la CLI.");
        Console.WriteLine();
        Console.WriteLine("  Arguments de démarrage :");
        Console.WriteLine("    --name, -n <nom>     : Nom BLE du serveur.");
        Console.WriteLine("    --excel, -e, -f <fic>: Fichier Excel à charger au lancement.");
        Console.WriteLine("    --mode, -m <mode>    : Mode sanda ou taolu.");
        Console.WriteLine("    --auto-start, -a     : Démarrer immédiatement la diffusion BLE.");
        Console.WriteLine();
    }

    private static async Task LoadExcelFileAsync(string filePath)
    {
        LogInfo($"Chargement du fichier Excel : '{filePath}'...");
        var result = ExcelCompetitionLoader.LoadFromFile(filePath);

        if (!result.Success)
        {
            LogError($"Échec du chargement : {result.ErrorMessage}");
            return;
        }

        if (result.Competition != null)
        {
            await _session!.LoadCompetitionAsync(result.Competition, result.Matches, result.Categories);
            LogSuccess("Compétition chargée avec succès !");
            Console.WriteLine($"  Nom           : {result.Competition.Name}");
            Console.WriteLine($"  Date          : {result.Competition.Date:dd/MM/yyyy}");
            Console.WriteLine($"  Discipline    : {result.Competition.Type}");
            Console.WriteLine($"  Catégories    : {result.Categories.Count}");
            Console.WriteLine($"  Compétiteurs  : {result.Competitors.Count}");
            Console.WriteLine($"  Matchs générés: {result.Matches.Count}");

            foreach (var w in result.Warnings)
            {
                LogWarning($"  [Avertissement] {w}");
            }

            if (result.Matches.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nPremiers matchs programmés (tapez 'match list' pour tout voir) :");
                Console.ResetColor();
                int previewCount = Math.Min(5, result.Matches.Count);
                for (int i = 0; i < previewCount; i++)
                {
                    var m = result.Matches[i];
                    string active = i == 0 ? " [*ACTIF*]" : "";
                    if (result.Competition.Type == CompetitionType.Taolu || m.Type == RoundType.Order)
                    {
                        Console.WriteLine($"  [{i + 1}] ID #{m.Id} | {m.Category} : {m.RedCompetitor}{active}");
                    }
                    else
                    {
                        Console.WriteLine($"  [{i + 1}] ID #{m.Id} | {m.Category} : {m.RedCompetitor} vs {m.BlueCompetitor}{active}");
                    }
                }
                if (result.Matches.Count > previewCount)
                {
                    Console.WriteLine($"  ... et {result.Matches.Count - previewCount} autre(s) match(s).");
                }
                Console.WriteLine();
            }
        }
    }

    private static void HandleExportCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- EXPORT EXCEL ---");
            Console.ResetColor();
            Console.WriteLine("  export results [fichier.xlsx] : Exporte les classements et résultats par catégorie.");
            Console.WriteLine("  export scores   [fichier.xlsx] : Exporte l'historique complet des notes des juges (format RefereeData).");
            Console.WriteLine("  export all                    : Exporte à la fois les résultats et les notes des juges.");
            Console.WriteLine("  export template [fichier.xlsx] : Génère un modèle vierge de compétition.");
            Console.WriteLine("\n  Exemples :");
            Console.WriteLine("    export results");
            Console.WriteLine("    export scores");
            Console.WriteLine("    export results C:\\Exports\\classement_final.xlsx\n");
            return;
        }

        string sub = args[0].ToLowerInvariant();
        string? targetPath = args.Length > 1 ? string.Join(" ", args.Skip(1)).Trim('\"', '\'') : null;

        switch (sub)
        {
            case "results":
            case "resultats":
            case "classement":
            case "res":
            case "r":
                ExportResults(targetPath);
                break;

            case "scores":
            case "notes":
            case "juges":
            case "judges":
            case "logs":
            case "s":
                ExportJudgeScores(targetPath);
                break;

            case "all":
            case "tout":
                ExportResults(null);
                ExportJudgeScores(null);
                break;

            case "template":
            case "tpl":
                ExportExcelTemplate(targetPath ?? "competition_template.xlsx");
                break;

            default:
                LogError($"Option d'export inconnue : '{sub}'. Options valides : results, scores, all, template.");
                break;
        }
    }

    private static void ExportResults(string? filePath)
    {
        try
        {
            LogInfo("Génération du fichier Excel des résultats...");
            string path = _session!.ExportResults(filePath);
            LogSuccess($"Résultats exportés avec succès dans : {path}");
        }
        catch (Exception ex)
        {
            LogError($"Erreur lors de l'export des résultats : {ex.Message}");
        }
    }

    private static void ExportJudgeScores(string? filePath)
    {
        try
        {
            LogInfo("Génération du fichier Excel des notes des juges...");
            string path = _session!.ExportJudgeScores(filePath);
            LogSuccess($"Notes des juges exportées avec succès dans : {path}");
        }
        catch (Exception ex)
        {
            LogError($"Erreur lors de l'export des notes : {ex.Message}");
        }
    }

    private static void ExportExcelTemplate(string filePath)
    {
        try
        {
            ExcelCompetitionLoader.ExportTemplate(filePath);
            LogSuccess($"Modèle Excel généré avec succès dans : {Path.GetFullPath(filePath)}");
            Console.WriteLine($"Remplissez ce fichier puis chargez-le avec : load \"{filePath}\"");
        }
        catch (Exception ex)
        {
            LogError($"Erreur lors de la génération du modèle : {ex.Message}");
        }
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
