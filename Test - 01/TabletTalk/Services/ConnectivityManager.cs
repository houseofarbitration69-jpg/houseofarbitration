using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TabletTalk.Services
{
    public enum ConnectivityMode
    {
        WiFi,
        Bluetooth
    }

    public class ConnectivityManager
    {
        private readonly WifiService _wifiService;
        private readonly BluetoothStarService _bluetoothService;
        private IConnectivityService _activeService;
        
        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<IEnumerable<string>> PeersFound;

        public ConnectivityMode CurrentMode { get; private set; }

        public ConnectivityManager(WifiService wifi, BluetoothStarService bluetooth)
        {
            _wifiService = wifi;
            _bluetoothService = bluetooth;
            
            // Relayer les événements du service actif
            _wifiService.MessageReceived += (s, e) => MessageReceived?.Invoke(s, e);
            _wifiService.StatusChanged += (s, e) => StatusChanged?.Invoke(s, e);
            _wifiService.PeersFound += (s, e) => PeersFound?.Invoke(s, e);
            
            _bluetoothService.MessageReceived += (s, e) => MessageReceived?.Invoke(s, e);
            _bluetoothService.StatusChanged += (s, e) => StatusChanged?.Invoke(s, e);
            _bluetoothService.PeersFound += (s, e) => PeersFound?.Invoke(s, e);

            // Par défaut, on commence en WiFi
            SetMode(ConnectivityMode.WiFi);
        }

        public async Task SetMode(ConnectivityMode mode)
        {
            if (_activeService != null)
            {
                await _activeService.Stop(); // Arrêter le service précédent
            }

            CurrentMode = mode;
            _activeService = mode == ConnectivityMode.WiFi ? _wifiService : _bluetoothService;
            StatusChanged?.Invoke(this, $"Mode changé en: {mode}");
        }
        
        public Task StartAsServer() => _activeService.Start(Role.Server);
        public Task ConnectToPeer(string peer) => _activeService.Start(Role.Client, peer);
        public Task ScanForPeers() => _activeService.ScanForPeers();
        public Task SendMessage(string message) => _activeService.SendMessage(message);
        public Task Stop() => _activeService.Stop();

        public async Task AutoConnect()
        {
            StatusChanged?.Invoke(this, "Tentative de connexion auto...");
            
            // 1. Tenter le WiFi en tant que client (nécessite une IP connue)
            // Pour une connexion auto sans IP connue, il faudrait implémenter mDNS/Zeroconf
            // ou une méthode de broadcast/découverte.
            // Pour l'instant, cette fonction tentera le WiFi en supposant qu'un serveur WiFi est démarré
            // et que l'IP est connue ou que l'utilisateur la renseignera après.
            
            // Si le WiFi n'est pas connecté ou échoue
            bool wifiSucceeded = false; // Placeholder for actual WiFi connection check
            
            // Une implémentation plus sophistiquée ici inclurait:
            // 1. Essayer de se connecter à une IP WiFi connue (si l'utilisateur en a configuré une)
            // 2. Lancer un scan mDNS pour trouver des serveurs WiFi (nécessite une bibliothèque mDNS)
            // Si rien n'est trouvé après un timeout...

            if (!wifiSucceeded) // Si le WiFi n'a pas pu établir de connexion automatique
            {
                StatusChanged?.Invoke(this, "Connexion WiFi auto impossible ou non configurée. Basculement sur Bluetooth.");
                await SetMode(ConnectivityMode.Bluetooth);
                await ScanForPeers(); // Lance le scan BT pour que l'utilisateur puisse choisir un Hub.
            }
            else
            {
                StatusChanged?.Invoke(this, "Connecté via WiFi (auto).");
            }
        }
    }
}