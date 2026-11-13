using System.Globalization;
using System.Text;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Judge.Cli;

internal static class Program
{
    private static readonly object ConsoleLock = new();
    private static WindowsBluetoothClient? _client;
    private static JudgeSession? _session;
    private static bool _running = true;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintBanner();

        _client = new WindowsBluetoothClient();
        _session = new JudgeSession(_client);

        // Abonnement aux événements du client et de la session
        HookSessionEvents(_session, _client);

        // Analyse des arguments en ligne de commande
        string? connectTarget = null;
        string? initialPosition = null;
        bool autoScan = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            if (arg is "-h" or "--help" or "/?")
            {
                PrintHelp();
                return 0;
            }
            if (arg is "-s" or "--scan")
            {
                autoScan = true;
            }
            else if (arg is "-c" or "--connect" && i + 1 < args.Length)
            {
                connectTarget = args[++i];
            }
            else if (arg is "-p" or "--pos" or "--position" && i + 1 < args.Length)
            {
                initialPosition = args[++i];
            }
        }

        // Si une position initiale a été précisée, l'appliquer dès maintenant
        if (!string.IsNullOrEmpty(initialPosition))
        {
            await SetPositionFromArgAsync(initialPosition);
        }

        if (autoScan)
        {
            await RunScanAsync(5);
        }

        if (!string.IsNullOrEmpty(connectTarget))
        {
            await TryConnectAsync(connectTarget);
        }

        // Boucle interactive REPL
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
        if (_session != null)
        {
            if (_client?.IsConnected == true)
            {
                try
                {
                    await _session.DisconnectJudgeAsync();
                    await Task.Delay(150);
                }
                catch { }
            }
            _session.Dispose();
        }

        LogInfo("CLI Arbitre fermée.");
        return 0;
    }

    private static void HookSessionEvents(JudgeSession session, WindowsBluetoothClient client)
    {
        client.LogMessage += (_, msg) => LogInfo($"[BLE] {msg}");

        session.StatusChanged += (_, msg) => LogInfo(msg);

        session.NotificationReceived += (_, msg) => LogServer(msg);

        session.PongReceived += (_, latency) =>
        {
            LogColor(ConsoleColor.Cyan, $"[PONG] Réponse du serveur en {latency.TotalMilliseconds:F0} ms");
        };

        session.MatchUpdated += (_, match) =>
        {
            LogColor(ConsoleColor.Green, $"[MATCH ACTIF] ID: #{match.Id} | Type: {match.Type} | Compétiteur: #{match.Competitor1Id}");
        };

        session.TimerUpdated += (_, timer) =>
        {
            LogColor(ConsoleColor.DarkYellow, $"[CHRONO] {timer} {(session.IsTimerRunning ? "(En cours)" : "(Arrêté)")}");
        };

        session.ServerStopped += (_, _) =>
        {
            lock (ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n========================================================");
                Console.WriteLine("  SERVEUR ARRÊTÉ — Le serveur d'arbitrage a stoppé.");
                Console.WriteLine("  L'écoute BLE est terminée. Fermeture du client.");
                Console.WriteLine("========================================================");
                Console.ResetColor();
            }
            _running = false;
        };
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"================================================================================");
        Console.WriteLine(@"        HOUSE OF ARBITRATION - CLI JUGE (BLUETOOTH LOW ENERGY WINDOWS)        ");
        Console.WriteLine(@"================================================================================");
        Console.ResetColor();
        Console.WriteLine("Outil en ligne de commande pour arbitrer sans tablette Android via le BLE Windows.\n");
    }

    private static void PrintHelpSummary()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Tapez 'scan' pour chercher le serveur, 'connect <#>' pour vous connecter, ou 'help' pour l'aide.");
        Console.ResetColor();
    }

    private static void PrintPrompt()
    {
        lock (ConsoleLock)
        {
            bool isConnected = _client?.IsConnected == true;
            bool hasUnsynced = _session?.HasUnsyncedChanges == true;
            bool isReconnecting = _session?.IsAutoReconnecting == true;

            string status;
            ConsoleColor statusColor;

            if (isConnected)
            {
                status = "CONNECTÉ";
                statusColor = ConsoleColor.Green;
            }
            else if (hasUnsynced)
            {
                status = $"HORS-LIGNE ({_session!.UnsyncedScores.Count} note(s) en attente{(isReconnecting ? ", reconnexion..." : "")})";
                statusColor = ConsoleColor.Yellow;
            }
            else if (isReconnecting)
            {
                status = "HORS-LIGNE (reconnexion en cours...)";
                statusColor = ConsoleColor.DarkYellow;
            }
            else
            {
                status = "DÉCONNECTÉ";
                statusColor = ConsoleColor.Red;
            }

            Console.ForegroundColor = statusColor;
            Console.Write($"[{status}");
            if (_session != null)
            {
                Console.Write($" | Juge {_session.PositionCode} | Score: {_session.CurrentScore:F2} (R={_session.RedPoints:F0} B={_session.BluePoints:F0})");
            }
            Console.Write("]");
            Console.ResetColor();
            Console.Write(" > ");
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
                case "scan":
                    int timeout = args.Length > 0 && int.TryParse(args[0], out int t) ? t : 5;
                    await RunScanAsync(timeout);
                    break;

                case "connect":
                case "c":
                    if (args.Length == 0)
                    {
                        if (_client?.LastConnectedBluetoothAddress.HasValue == true)
                        {
                            LogInfo($"Reconnexion au dernier serveur connu [{_client.LastConnectedDeviceAddress}]...");
                            await _client.ReconnectAsync();
                            return;
                        }
                        LogError("Usage: connect <index_du_scan | adresse_bluetooth>");
                        return;
                    }
                    await TryConnectAsync(args[0]);
                    break;

                case "reconnect":
                case "rec":
                    if (_client != null)
                    {
                        LogInfo("Tentative de reconnexion...");
                        await _client.ReconnectAsync();
                    }
                    break;

                case "sync":
                    if (_session != null)
                    {
                        await _session.SyncUnsyncedScoresAsync();
                    }
                    break;

                case "disconnect":
                case "disc":
                    if (_session != null)
                    {
                        try
                        {
                            await _session.DisconnectJudgeAsync();
                            await Task.Delay(150);
                        }
                        catch { }
                        if (_client != null)
                        {
                            await _client.DisconnectAsync();
                        }
                        LogInfo("Déconnecté.");
                    }
                    break;

                case "pos":
                case "position":
                    if (args.Length == 0)
                    {
                        LogError("Usage: pos <groupe> <numéro> (ex: pos A 1) OU pos <numéro_sanda> (ex: pos 1)");
                        return;
                    }
                    await HandleSetPositionAsync(args);
                    break;

                case "code":
                    if (args.Length == 0)
                    {
                        LogError("Usage: code <code_taolu> (ex: code 01, code 30, code 50)");
                        return;
                    }
                    await ApplyCodeAsync(args[0]);
                    break;

                case "deduct":
                case "d":
                    if (args.Length == 0 || !double.TryParse(args[0].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double deductVal))
                    {
                        LogError("Usage: deduct <valeur> [motif] (ex: deduct 0.2 Sortie de tapis)");
                        return;
                    }
                    string reason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Déduction manuelle";
                    await _session!.DeductPointsAsync(deductVal, reason);
                    break;

                case "score":
                case "s":
                    if (args.Length == 0 || !double.TryParse(args[0].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double exactScore))
                    {
                        LogError("Usage: score <score_absolu> (ex: score 4.85)");
                        return;
                    }
                    await _session!.SetAbsoluteScoreAsync(exactScore);
                    break;

                case "red":
                case "+r":
                case "r":
                    await _session!.AddSandaPointAsync(Constants.Side.Red);
                    break;

                case "blue":
                case "+b":
                case "b":
                    await _session!.AddSandaPointAsync(Constants.Side.Blue);
                    break;

                case "match":
                case "m":
                    await _session!.RequestMatchAsync();
                    break;

                case "comp":
                    await _session!.RequestCompetitionAsync();
                    break;

                case "codes":
                    ShowTaoluCodes(args.Length > 0 ? args[0] : null);
                    break;

                case "history":
                case "hist":
                    ShowHistory();
                    break;

                case "ping":
                    LogInfo("Envoi PING...");
                    await _session!.PingAsync();
                    break;

                case "watchdog":
                    if (args.Length > 0 && args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        _session!.StopWatchdog();
                    }
                    else
                    {
                        int interval = args.Length > 0 && int.TryParse(args[0], out int sec) ? sec : 4;
                        _session!.StartWatchdog(interval);
                    }
                    break;

                case "raw":
                    if (args.Length == 0)
                    {
                        LogError("Usage: raw <message_brut>");
                        return;
                    }
                    string rawMsg = string.Join(" ", args);
                    await _session!.SendRawAsync(rawMsg);
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
                    LogError($"Commande inconnue : '{cmd}'. Tapez 'help' pour voir la liste.");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError($"Erreur lors de l'exécution de la commande : {ex.Message}");
        }
    }

    private static async Task RunScanAsync(int timeoutSeconds)
    {
        LogInfo($"Démarrage du scan BLE pendant {timeoutSeconds} secondes...");
        var devices = await _client!.ScanForDevicesAsync(TimeSpan.FromSeconds(timeoutSeconds));

        if (devices.Count == 0)
        {
            LogWarning("Aucun périphérique BLE détecté avec le service d'arbitrage.");
            LogInfo("Assurez-vous que l'application Serveur d'arbitrage est démarrée et diffuse le service BLE.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- {devices.Count} PÉRIPHÉRIQUE(S) TROUVÉ(S) ---");
        Console.ResetColor();

        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            string marker = d.HasArbitrationService ? "[SERVEUR ARBITRATION]" : "";
            Console.WriteLine($"  [{i + 1}] {d.DisplayName} ({d.AddressFormatted}) RSSI: {d.Rssi} dBm {marker}");
        }
        Console.WriteLine();
        LogInfo("Tapez 'connect 1' (ou l'index de votre choix) pour vous connecter.");
    }

    private static async Task TryConnectAsync(string target)
    {
        // 1. Vérifie si la cible est un index dans la dernière liste de scan
        if (int.TryParse(target, out int index) && _client!.DiscoveredDevices.Count > 0)
        {
            if (index >= 1 && index <= _client.DiscoveredDevices.Count)
            {
                var device = _client.DiscoveredDevices[index - 1];
                LogInfo($"Connexion à [{device.DisplayName}] ({device.AddressFormatted}, Type: {device.AddressType})...");
                bool ok = await _client.ConnectAsync(device);
                await OnConnectionResultAsync(ok, device.DisplayName);
                return;
            }
            LogError($"Index invalide: {index}. Les index valides vont de 1 à {_client.DiscoveredDevices.Count}.");
            return;
        }

        // 2. Tente de parser une adresse bluetooth hexadécimale ou numérique
        string cleanAddr = target.Replace(":", "").Replace("-", "").Trim();
        if (ulong.TryParse(cleanAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong address))
        {
            LogInfo($"Connexion à l'adresse 0x{address:X12}...");
            bool ok = await _client!.ConnectAsync(address);
            await OnConnectionResultAsync(ok, $"0x{address:X12}");
            return;
        }

        // 3. Recherche par nom parmi les périphériques scannés
        var matchByName = _client!.DiscoveredDevices.FirstOrDefault(d =>
            d.Name.Contains(target, StringComparison.OrdinalIgnoreCase) ||
            d.DisplayName.Contains(target, StringComparison.OrdinalIgnoreCase));
        if (matchByName != null)
        {
            LogInfo($"Périphérique '{matchByName.DisplayName}' trouvé dans le cache de scan. Connexion...");
            bool ok = await _client.ConnectAsync(matchByName);
            await OnConnectionResultAsync(ok, matchByName.DisplayName);
            return;
        }

        LogError($"Impossible de résoudre la cible '{target}'. Utilisez d'abord 'scan' ou fournissez une adresse Bluetooth valide.");
    }

    private static async Task OnConnectionResultAsync(bool success, string deviceLabel)
    {
        if (success)
        {
            LogSuccess($"Connecté avec succès à {deviceLabel} !");

            // Maintien actif de la liaison
            _session!.StartWatchdog(4);

            // Transmission immédiate de la position pour que le serveur allume le statut du juge !
            LogInfo($"Officialisation de la présence : Juge {_session.PositionCode}...");
            await _session.SendCurrentPositionAsync();

            // Demande des données de match et de compétition
            await _session.RequestMatchAsync();
            await _session.RequestCompetitionAsync();
        }
        else
        {
            LogError($"Échec de la connexion à {deviceLabel}. Vérifiez que le serveur est bien allumé et à portée.");
        }
    }

    private static async Task HandleSetPositionAsync(string[] args)
    {
        if (args.Length == 1)
        {
            await SetPositionFromArgAsync(args[0]);
        }
        else if (args.Length >= 2)
        {
            string group = args[0].ToUpperInvariant();
            if (int.TryParse(args[1], out int pos))
            {
                await _session!.SetPositionAsync(group, pos);
            }
            else
            {
                LogError($"Numéro de position invalide : '{args[1]}'.");
            }
        }
    }

    private static async Task SetPositionFromArgAsync(string posArg)
    {
        string trimmed = posArg.Trim().ToUpperInvariant();
        if (trimmed.Contains('_'))
        {
            string[] sub = trimmed.Split('_');
            if (sub.Length == 2 && int.TryParse(sub[1], out int p))
            {
                await _session!.SetPositionAsync(sub[0], p);
                return;
            }
        }
        else if (trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && int.TryParse(trimmed.Substring(1), out int p))
        {
            await _session!.SetPositionAsync(trimmed[0].ToString(), p);
            return;
        }
        else if (int.TryParse(trimmed, out int sandaPos))
        {
            await _session!.SetPositionAsync(string.Empty, sandaPos);
            return;
        }

        LogError($"Format de position non reconnu : '{posArg}'. Exemples: 'A 1', 'A_1', 'B_2', '1'.");
    }

    private static async Task ApplyCodeAsync(string code)
    {
        bool success = await _session!.ApplyTaoluCodeAsync(code);
        if (!success)
        {
            LogError("Échec de l'envoi de la déduction par code.");
        }
    }

    private static void ShowTaoluCodes(string? filter)
    {
        var codes = MvtCodeModel.DefaultCodes.AsEnumerable();
        if (!string.IsNullOrEmpty(filter))
        {
            codes = codes.Where(c =>
                c.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.Movement.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n--- CODES DE PÉNALITÉ TAOLU DISPONIBLES ---");
        Console.ResetColor();

        foreach (var c in codes)
        {
            Console.WriteLine($"  Code [{c.Code,-4}] | Pénalité: -{c.Value:F1} | Cat: {c.Category,-12} | {c.Movement}");
        }
        Console.WriteLine();
    }

    private static void ShowHistory()
    {
        if (_session!.DeductionsHistory.Count == 0)
        {
            LogInfo("Aucune déduction effectuée pour le moment.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n--- HISTORIQUE DES DÉDUCTIONS ({_session.DeductionsHistory.Count}) ---");
        Console.ResetColor();

        foreach (var item in _session.DeductionsHistory)
        {
            Console.WriteLine($"  {item.Date:HH:mm:ss} | Code: {item.Code?.Code,-4} (-{item.Code?.Value:F1}) | Score résultant: {item.Score:F2} | {item.Code?.Movement}");
        }
        Console.WriteLine();
    }

    private static void PrintStatus()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n--- ÉTAT DE LA SESSION D'ARBITRAGE ---");
        Console.ResetColor();

        string activeServerName = !string.IsNullOrWhiteSpace(_session?.ServerName)
            ? _session.ServerName
            : (!string.IsNullOrWhiteSpace(_client?.ConnectedDeviceName) ? _client.ConnectedDeviceName : "Serveur BLE");
        Console.WriteLine($"  Connexion BLE    : {(_client!.IsConnected ? "CONNECTÉ (" + activeServerName + ")" : "DÉCONNECTÉ")}");
        Console.WriteLine($"  Position juge    : {_session!.PositionCode} (Groupe '{_session.JudgeGroup}', Numéro {_session.JudgePosition})");
        Console.WriteLine($"  Score courant    : {_session.CurrentScore:F2}");
        Console.WriteLine($"  Points Sanda     : Rouge={_session.RedPoints:F0} | Bleu={_session.BluePoints:F0}");
        Console.WriteLine($"  Chronomètre      : {_session.TimerDisplay} {(_session.IsTimerRunning ? "[ACTIF]" : "[STOP]")}");
        Console.WriteLine($"  Watchdog ping    : {(_session.IsWatchdogActive ? "ACTIF (toutes les 4s)" : "INACTIF")}");

        if (_session.CurrentMatch != null)
        {
            Console.WriteLine($"  Match actuel     : #{_session.CurrentMatch.Id} ({_session.CurrentMatch.Type})");
            Console.WriteLine($"  Compétiteur 1    : #{_session.CurrentMatch.Competitor1Id} {_session.CurrentMatch.Competitor1Name}");
        }
        else
        {
            Console.WriteLine("  Match actuel     : Aucun match actif");
        }

        if (_session.CurrentCompetition != null)
        {
            Console.WriteLine($"  Compétition      : {_session.CurrentCompetition.Name}");
        }
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n--- COMMANDES DISPONIBLES ---");
        Console.ResetColor();
        Console.WriteLine("  scan [sec]             : Scanne les serveurs d'arbitrage BLE (défaut: 5s).");
        Console.WriteLine("  connect <#|addr>       : Connecte au périphérique scanné par numéro ou adresse.");
        Console.WriteLine("  disconnect             : Déconnecte la liaison BLE.");
        Console.WriteLine("  pos <groupe> <num>     : Spécifie la position (ex: pos A 1, pos B 2, pos 1).");
        Console.WriteLine("  code <code>            : Applique une pénalité Taolu (ex: code 01, code 30, code 50).");
        Console.WriteLine("  deduct <points> [mot]  : Déduit un score direct (ex: deduct 0.2, deduct 0.1 sortie).");
        Console.WriteLine("  score <valeur>         : Définit le score absolu du compétiteur (ex: score 4.85).");
        Console.WriteLine("  red, +r                : Ajoute 1 point au coin ROUGE (Sanda).");
        Console.WriteLine("  blue, +b               : Ajoute 1 point au coin BLEU (Sanda).");
        Console.WriteLine("  match, m               : Demande les infos du match en cours au serveur.");
        Console.WriteLine("  comp                   : Demande les données de la compétition.");
        Console.WriteLine("  codes [filtre]         : Affiche la liste des codes de mouvements Taolu.");
        Console.WriteLine("  history                : Affiche l'historique des déductions du match en cours.");
        Console.WriteLine("  ping                   : Envoie un ping et mesure la latence aller-retour.");
        Console.WriteLine("  watchdog <on|off|sec>  : Active/désactive le ping keep-alive en arrière-plan.");
        Console.WriteLine("  raw <message>          : Envoie une chaîne brute vers le serveur.");
        Console.WriteLine("  status                 : Affiche l'état courant de la session.");
        Console.WriteLine("  clear, cls             : Efface l'écran.");
        Console.WriteLine("  quit, exit             : Quitte la CLI.");
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

    private static void LogServer(string message)
    {
        LogColor(ConsoleColor.Magenta, $"[SERVEUR] {message}");
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
