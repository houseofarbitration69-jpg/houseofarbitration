using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent; // Added for thread-safe collection

namespace TabletTalk.Services // Added namespace to avoid conflicts
{
    public class WifiService : IConnectivityService
    {
        private TcpListener _listener;
        private readonly ConcurrentBag<TcpClient> _clients = new(); // Use ConcurrentBag for thread safety
        private TcpClient _client;
        private const int Port = 8888;
        private string _deviceId = DeviceInfo.Name;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<IEnumerable<string>> PeersFound; // Non implémenté pour le WiFi (nécessite mDNS)

        public async Task Start(Role role, string peerToConnect = null)
        {
            await Stop(); // Ensure previous connections are stopped

            if (role == Role.Server)
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                StatusChanged?.Invoke(this, $"Serveur démarré à {GetLocalIPAddress()}:{Port}");
                _ = AcceptClientsAsync();
            }
            else
            {
                if (string.IsNullOrEmpty(peerToConnect))
                {
                    StatusChanged?.Invoke(this, "L'adresse IP du serveur est requise.");
                    return;
                }
                _client = new TcpClient();
                try
                {
                    await _client.ConnectAsync(IPAddress.Parse(peerToConnect), Port);
                    StatusChanged?.Invoke(this, $"Connecté au serveur {peerToConnect}");
                    _ = ListenForMessagesAsync(_client);
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Échec de la connexion au serveur {peerToConnect}: {ex.Message}");
                    _client?.Close();
                    _client = null;
                }
            }
        }

        public async Task SendMessage(string messageContent)
        {
            var chatMessage = new ChatMessage { SenderId = _deviceId, Content = messageContent };
            var jsonMessage = JsonConvert.SerializeObject(chatMessage);

            if (_listener != null) // Mode Serveur
            {
                await Broadcast(jsonMessage);
            }
            else if (_client != null && _client.Connected) // Mode Client
            {
                await SendToStream(_client, jsonMessage);
            }
            else
            {
                StatusChanged?.Invoke(this, "Non connecté pour envoyer un message.");
            }
        }

        private async Task AcceptClientsAsync()
        {
            while (_listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _clients.Add(client);
                    StatusChanged?.Invoke(this, $"Client connecté: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
                    _ = ListenForMessagesAsync(client);
                }
                catch (ObjectDisposedException) // Listener a été arrêté
                {
                    break;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Erreur AcceptClientsAsync: {ex.Message}");
                }
            }
        }

        private async Task ListenForMessagesAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            try
            {
                while (client.Connected)
                {
                    var jsonMessage = await reader.ReadLineAsync();
                    if (jsonMessage == null) break; // Client déconnecté

                    // En mode serveur, relayer le message aux autres clients
                    if (_listener != null)
                    {
                        await Broadcast(jsonMessage, client);
                    }
                    
                    var chatMessage = JsonConvert.DeserializeObject<ChatMessage>(jsonMessage);
                    // Invoke on MainThread as MessageReceived handlers might update UI
                    MainThread.BeginInvokeOnMainThread(() => MessageReceived?.Invoke(this, $"{chatMessage.SenderId}: {chatMessage.Content}"));
                }
            }
            catch (IOException) // Client déconnecté de force ou erreur réseau
            {
                // Expected when client disconnects
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Erreur ListenForMessagesAsync: {ex.Message}");
            }
            finally
            {
                _clients.TryTake(out client); // Remove client from bag
                client?.Close();
                StatusChanged?.Invoke(this, "Un client s'est déconnecté.");
            }
        }

        private async Task Broadcast(string jsonMessage, TcpClient excludeClient = null)
        {
            var tasks = new List<Task>();
            foreach (var client in _clients)
            {
                if (client != excludeClient && client.Connected)
                {
                    tasks.Add(SendToStream(client, jsonMessage));
                }
            }
            await Task.WhenAll(tasks);
        }

        private async Task SendToStream(TcpClient client, string json)
        {
            try
            {
                var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
                await writer.WriteLineAsync(json);
            }
            catch (IOException) // Client déconnecté pendant l'envoi
            {
                StatusChanged?.Invoke(this, "Erreur d'envoi: client déconnecté.");
                client.Close();
                _clients.TryTake(out client); // Remove disconnected client
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Erreur d'envoi: {ex.Message}");
            }
        }

        public async Task Stop()
        {
            _listener?.Stop();
            _listener = null;
            
            foreach (var client in _clients)
            {
                client.Close();
            }
            _clients.Clear();

            _client?.Close();
            _client = null;
            StatusChanged?.Invoke(this, "Service WiFi arrêté.");
            await Task.CompletedTask;
        }

        public Task ScanForPeers()
        {
            StatusChanged?.Invoke(this, "Scan non supporté en WiFi sans mDNS. Entrez l'IP manuellement.");
            return Task.CompletedTask;
        }
        
        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "IP non trouvée";
        }
    }
}