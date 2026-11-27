using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Judge.Cli;

/// <summary>
/// Gestionnaire permettant d'instancier, connecter et coordonner plusieurs robots juges simultanément
/// pour simuler une table complète d'arbitrage (Sanda : 1..5, Taolu : A1..A3, B1..B3, C1..C3).
/// </summary>
public class JudgeBotManager : IDisposable
{
    private static readonly (string Group, int Position)[] TaoluPositions =
    {
        ("A", 1), ("A", 2), ("A", 3),
        ("B", 1), ("B", 2), ("B", 3),
        ("C", 1), ("C", 2), ("C", 3)
    };

    private readonly List<(WindowsBluetoothClient Client, JudgeSession Session, JudgeBot Bot)> _managedBots = new();

    public IReadOnlyList<JudgeBot> Bots => _managedBots.Select(b => b.Bot).ToList().AsReadOnly();
    public int Count => _managedBots.Count;

    public event EventHandler<string>? ManagerActivity;

    /// <summary>
    /// Retourne le couple (Groupe, Numéro) pour un robot d'index donné (base 0) selon le mode (Taolu ou Sanda).
    /// </summary>
    public static (string Group, int Position) GetBotPosition(int zeroBasedIndex, bool isTaolu)
    {
        if (isTaolu)
        {
            int idx = Math.Clamp(zeroBasedIndex, 0, TaoluPositions.Length - 1);
            return TaoluPositions[idx];
        }

        int sandaPos = Math.Clamp(zeroBasedIndex + 1, 1, 5);
        return (string.Empty, sandaPos);
    }

    /// <summary>
    /// Crée et connecte un groupe de robots juges avec des positions adaptées au type de compétition
    /// (Sanda : 1..5, Taolu : A_1..A_3, B_1..B_3, C_1..C_3).
    /// Si serverAddressOrName est vide ou un nom, recherche automatiquement le serveur BLE.
    /// </summary>
    public async Task<int> ConnectBotsAsync(string? serverAddressOrName = null, int count = 3, int speedMultiplier = 1, string strategy = "balanced")
    {
        count = Math.Clamp(count, 1, 9);
        int connectedCount = 0;

        ulong? targetAddress = null;
        Windows.Devices.Bluetooth.BluetoothAddressType targetAddressType = Windows.Devices.Bluetooth.BluetoothAddressType.Public;

        if (!string.IsNullOrWhiteSpace(serverAddressOrName) &&
            WindowsBluetoothClient.TryParseAddress(serverAddressOrName, out ulong directAddr))
        {
            targetAddress = directAddr;
        }
        else
        {
            ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Recherche du serveur BLE{(string.IsNullOrWhiteSpace(serverAddressOrName) ? "" : $" '{serverAddressOrName}'")}...");
            using var scanner = new WindowsBluetoothClient();
            var found = await scanner.ScanForServerAsync(TimeSpan.FromSeconds(3));
            if (found != null)
            {
                targetAddress = found.Address;
                targetAddressType = found.AddressType;
                ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Serveur trouvé : [{found.DisplayName}] ({found.AddressFormatted})");
            }
            else
            {
                ManagerActivity?.Invoke(this, "[BOT-MANAGER] Aucun serveur d'arbitrage BLE trouvé après scan.");
                return 0;
            }
        }

        bool? detectedTaolu = null;
        int effectiveCount = count;

        for (int i = 0; i < effectiveCount; i++)
        {
            var client = new WindowsBluetoothClient();
            var session = new JudgeSession(client);
            var bot = new JudgeBot(session)
            {
                SpeedMultiplier = speedMultiplier,
                Strategy = strategy
            };

            bot.BotActivity += (s, msg) => ManagerActivity?.Invoke(this, msg);

            if (detectedTaolu.HasValue)
            {
                var (grp, num) = GetBotPosition(i, detectedTaolu.Value);
                // Pré-affecter la position avant connexion pour éviter un repli par défaut
                if (!string.IsNullOrEmpty(grp))
                {
                    await session.SetPositionAsync(grp, num);
                }
                else
                {
                    session.JudgePosition = num;
                }
            }

            ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Connexion du robot #{i + 1}...");
            bool ok = await client.ConnectAsync(targetAddress.Value, targetAddressType);
            if (ok)
            {
                connectedCount++;
                session.StartWatchdog();

                if (!detectedTaolu.HasValue)
                {
                    // Attendre brièvement les infos de compétition pour détecter Sanda vs Taolu
                    await session.RequestCompetitionAsync();
                    for (int wait = 0; wait < 25; wait++)
                    {
                        if (session.CurrentCompetition != null || session.CurrentMatch != null)
                            break;
                        await Task.Delay(60);
                    }

                    detectedTaolu = session.CurrentCompetition?.Type == CompetitionType.Taolu
                                 || session.CurrentMatch?.Type == RoundType.Order;

                    effectiveCount = detectedTaolu.Value ? Math.Min(count, 9) : Math.Min(count, 5);
                    ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Mode détecté : {(detectedTaolu.Value ? "Taolu (A1..C3)" : "Sanda (1..5)")}.");
                }

                var (group, number) = GetBotPosition(i, detectedTaolu.Value);
                await session.SetPositionAsync(group, number);

                ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Robot #{i + 1} (Juge {session.PositionCode}) connecté et position enregistrée.");
                bot.Start(strategy, speedMultiplier);
            }
            else
            {
                ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Échec de connexion pour le robot #{i + 1}.");
            }

            _managedBots.Add((client, session, bot));
            await Task.Delay(100);
        }

        return connectedCount;
    }

    /// <summary>
    /// Ajoute manuellement un robot pour gestion et coordination (utile pour les tests et configurations avancées).
    /// </summary>
    public JudgeBot AddBot(WindowsBluetoothClient client, JudgeSession session, int speedMultiplier = 1, string strategy = "balanced")
    {
        var bot = new JudgeBot(session)
        {
            SpeedMultiplier = speedMultiplier,
            Strategy = strategy
        };
        bot.BotActivity += (s, msg) => ManagerActivity?.Invoke(this, msg);
        _managedBots.Add((client, session, bot));
        return bot;
    }

    /// <summary>
    /// Démarre tous les robots configurés.
    /// </summary>
    public void StartAll(string? strategy = null, int? speedMultiplier = null)
    {
        foreach (var item in _managedBots)
        {
            item.Bot.Start(strategy, speedMultiplier);
        }
        ManagerActivity?.Invoke(this, $"[BOT-MANAGER] {Count} robot(s) démarré(s).");
    }

    /// <summary>
    /// Arrête tous les robots configurés.
    /// </summary>
    public void StopAll()
    {
        foreach (var item in _managedBots)
        {
            item.Bot.Stop();
        }
        ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Tous les robots sont arrêtés.");
    }

    /// <summary>
    /// Met à jour la vitesse pour tous les robots.
    /// </summary>
    public void SetSpeedMultiplierAll(int multiplier)
    {
        foreach (var item in _managedBots)
        {
            item.Bot.SpeedMultiplier = multiplier;
        }
        ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Vitesse des robots réglée à x{multiplier}.");
    }

    /// <summary>
    /// Met à jour la stratégie pour tous les robots.
    /// </summary>
    public void SetStrategyAll(string strategy)
    {
        foreach (var item in _managedBots)
        {
            item.Bot.Strategy = strategy;
        }
        ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Stratégie des robots réglée à '{strategy}'.");
    }

    public void Dispose()
    {
        StopAll();
        foreach (var item in _managedBots)
        {
            try
            {
                item.Bot.Dispose();
                item.Session.Dispose();
                item.Client.Dispose();
            }
            catch { }
        }
        _managedBots.Clear();
        GC.SuppressFinalize(this);
    }
}
