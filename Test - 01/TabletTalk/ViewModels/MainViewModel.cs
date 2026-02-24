using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TabletTalk.Services; // Assurez-vous d'avoir le bon namespace

namespace TabletTalk.ViewModels // Added namespace
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ConnectivityManager _connectivityManager;

        [ObservableProperty] private string statusText = "Prêt.";
        [ObservableProperty] private string messageToSend;
        [ObservableProperty] private string peerInput; // Pour l'IP WiFi ou le nom BT

        public ObservableCollection<string> Messages { get; } = new();
        public ObservableCollection<string> FoundPeers { get; } = new();

        public MainViewModel(ConnectivityManager manager)
        {
            _connectivityManager = manager;
            _connectivityManager.StatusChanged += (s, e) => MainThread.BeginInvokeOnMainThread(() => StatusText = e);
            _connectivityManager.MessageReceived += (s, e) => MainThread.BeginInvokeOnMainThread(() => Messages.Add(e));
            _connectivityManager.PeersFound += (s, peers) => MainThread.BeginInvokeOnMainThread(() =>
            {
                FoundPeers.Clear();
                foreach(var p in peers) FoundPeers.Add(p);
            });
        }

        [RelayCommand]
        async Task SetMode(string mode)
        {
            var newMode = mode == "WiFi" ? ConnectivityMode.WiFi : ConnectivityMode.Bluetooth;
            await _connectivityManager.SetMode(newMode);
        }

        [RelayCommand] async Task StartServer() => await _connectivityManager.StartAsServer();
        [RelayCommand] async Task Scan() => await _connectivityManager.ScanForPeers();
        [RelayCommand] async Task Connect() => await _connectivityManager.ConnectToPeer(PeerInput);
        [RelayCommand] async Task AutoConnect() => await _connectivityManager.AutoConnect();

        [RelayCommand]
        async Task Send()
        {
            if (string.IsNullOrWhiteSpace(MessageToSend)) return;
            await _connectivityManager.SendMessage(MessageToSend);
            MainThread.BeginInvokeOnMainThread(() => Messages.Add($"Moi: {MessageToSend}"));
            MessageToSend = string.Empty;
        }
    }
}