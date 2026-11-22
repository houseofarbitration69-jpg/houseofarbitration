namespace House.Of.Arbitration.Judge.Cli;

/// <summary>
/// Gestionnaire permettant d'instancier, connecter et coordonner plusieurs robots juges simultanément
/// pour simuler une table complète d'arbitrage (ex: 3 ou 5 juges Sanda).
/// </summary>
public class JudgeBotManager : IDisposable
{
    private readonly List<(WindowsBluetoothClient Client, JudgeSession Session, JudgeBot Bot)> _managedBots = new();

    public IReadOnlyList<JudgeBot> Bots => _managedBots.Select(b => b.Bot).ToList().AsReadOnly();
    public int Count => _managedBots.Count;

    public event EventHandler<string>? ManagerActivity;

    /// <summary>
    /// Crée et connecte un groupe de robots juges avec des positions séquentielles (1..count).
    /// Si serverAddressOrName est vide ou un nom, recherche automatiquement le serveur BLE.
    /// </summary>
    public async Task<int> ConnectBotsAsync(string? serverAddressOrName = null, int count = 3, int speedMultiplier = 1, string strategy = "balanced")
    {
        count = Math.Clamp(count, 1, 5);
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

        for (int i = 1; i <= count; i++)
        {
            var client = new WindowsBluetoothClient();
            var session = new JudgeSession(client);
            var bot = new JudgeBot(session)
            {
                SpeedMultiplier = speedMultiplier,
                Strategy = strategy
            };

            bot.BotActivity += (s, msg) => ManagerActivity?.Invoke(this, msg);

            // Pré-affecter le numéro du juge
            session.JudgePosition = i;

            ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Connexion du robot #{i} (Juge {i})...");
            bool ok = await client.ConnectAsync(targetAddress.Value, targetAddressType);
            if (ok)
            {
                connectedCount++;
                session.StartWatchdog();

                ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Robot #{i} (Juge {i}) connecté et position enregistrée.");
                bot.Start(strategy, speedMultiplier);
            }
            else
            {
                ManagerActivity?.Invoke(this, $"[BOT-MANAGER] Échec de connexion pour le robot #{i}.");
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
