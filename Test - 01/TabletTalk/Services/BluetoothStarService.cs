using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE;

namespace TabletTalk.Services
{
    public class BluetoothStarService : IConnectivityService
    {
        private readonly IBluetoothLE _ble = CrossBluetoothLE.Current;
        private readonly IAdapter _adapter;
        private readonly IGattServer _gattServer;
        private readonly string _deviceId = DeviceInfo.Name;

        public static readonly Guid ServiceGuid = new Guid("A7EEDF2C-D47A-4752-9E9C-1430485920A4");
        public static readonly Guid ChatCharacteristicGuid = new Guid("B7EEDF2C-D47A-4752-9E9C-1430485920A4");

        private IDevice _hubDevice;
        private ICharacteristic _chatCharacteristic;
        private CancellationTokenSource _scanCancellationTokenSource;
        private ConcurrentDictionary<Guid, IDevice> _foundDevices = new ConcurrentDictionary<Guid, IDevice>();
        private Role _currentRole;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<IEnumerable<string>> PeersFound;
        
        public BluetoothStarService(IGattServer gattServer)
        {
            _gattServer = gattServer;
            _adapter = _ble.Adapter;

            // Wire up events
            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            _adapter.DeviceConnected += OnDeviceConnected;
            _adapter.DeviceDisconnected += OnDeviceDisconnected;
            _ble.StateChanged += OnBleStateChanged;
            _gattServer.MessageReceived += OnGattMessageReceived;
            _gattServer.StatusChanged += (s, e) => StatusChanged?.Invoke(s, e);
        }

        private void OnGattMessageReceived(object sender, byte[] e)
        {
            // When the Hub receives a message, it needs to be broadcasted
            // And also displayed on the Hub's screen.
            var json = Encoding.UTF8.GetString(e);
            var chatMessage = JsonConvert.DeserializeObject<ChatMessage>(json);
            if (chatMessage != null)
            {
                MessageReceived?.Invoke(this, $"{chatMessage.SenderId}: {chatMessage.Content}");
                // Broadcast to other subscribers
                _gattServer.SendMessageToSubscribers(e);
            }
        }

        private void OnBleStateChanged(object sender, BluetoothStateChangedArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, $"État Bluetooth: {e.NewState}"));
        }

        private void OnDeviceDisconnected(object sender, DeviceEventArgs e)
        {
            if (e.Device.Id == _hubDevice?.Id)
            {
                MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, $"Déconnecté du Hub BT: {e.Device.Name}"));
                _hubDevice = null;
                _chatCharacteristic = null;
            }
        }

        private async void OnDeviceConnected(object sender, DeviceEventArgs e)
        {
            if (e.Device.Id == _hubDevice?.Id)
            {
                MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, $"Connecté au Hub BT: {e.Device.Name}"));
                
                try
                {
                    var service = await _hubDevice.GetServiceAsync(ServiceGuid);
                    if (service != null)
                    {
                        _chatCharacteristic = await service.GetCharacteristicAsync(ChatCharacteristicGuid);
                        if (_chatCharacteristic != null && _chatCharacteristic.CanUpdate)
                        {
                            _chatCharacteristic.ValueUpdated += OnCharacteristicValueUpdated;
                            await _chatCharacteristic.StartUpdatesAsync();
                            MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, "Abonné aux messages du Hub BT."));
                        }
                    }
                }
                catch (Exception ex)
                {
                    MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, $"Erreur lors de la découverte du service BT: {ex.Message}"));
                }
            }
        }

        private void OnCharacteristicValueUpdated(object sender, CharacteristicUpdatedEventArgs e)
        {
            var json = Encoding.UTF8.GetString(e.Characteristic.Value);
            var chatMessage = JsonConvert.DeserializeObject<ChatMessage>(json);
            if (chatMessage != null)
            {
                MainThread.BeginInvokeOnMainThread(() => MessageReceived?.Invoke(this, $"{chatMessage.SenderId}: {chatMessage.Content}"));
            }
        }

        public async Task Start(Role role, string peerToConnect = null)
        {
            await Stop(); // Stop any ongoing operations
            _currentRole = role;

            var permissionGranted = await RequestBluetoothPermissions();
            if (!permissionGranted)
            {
                StatusChanged?.Invoke(this, "Permissions Bluetooth non accordées.");
                return;
            }
            
            if (_ble.State != BluetoothState.On)
            {
                StatusChanged?.Invoke(this, "Bluetooth est désactivé ou non prêt.");
                return;
            }

            if (role == Role.Server)
            {
                await _gattServer.Start(ServiceGuid, ChatCharacteristicGuid);
            }
            else // Client (Spoke)
            {
                if (string.IsNullOrEmpty(peerToConnect))
                {
                    StatusChanged?.Invoke(this, "Un identifiant de Hub est requis pour la connexion.");
                    return;
                }
                await ConnectToPeer(peerToConnect);
            }
        }

        public async Task ScanForPeers()
        {
            var permissionGranted = await RequestBluetoothPermissions();
            if (!permissionGranted)
            {
                StatusChanged?.Invoke(this, "Permissions Bluetooth non accordées. Impossible de scanner.");
                return;
            }

            if (_ble.State != BluetoothState.On)
            {
                StatusChanged?.Invoke(this, "Bluetooth est désactivé.");
                return;
            }

            _foundDevices.Clear();
            _scanCancellationTokenSource = new CancellationTokenSource();
            
            MainThread.BeginInvokeOnMainThread(() => PeersFound?.Invoke(this, new List<string>()));
            StatusChanged?.Invoke(this, "Scan des Hubs Bluetooth en cours...");
            
            await _adapter.StartScanningForDevicesAsync(scanFilterOptions: new ScanFilterOptions { ServiceUuids = new Guid[] { ServiceGuid } }, cancellationToken: _scanCancellationTokenSource.Token);

            _ = Task.Delay(10000, _scanCancellationTokenSource.Token).ContinueWith(t => {
                if (!t.IsCanceled)
                {
                    _adapter.StopScanningForDevicesAsync();
                    StatusChanged?.Invoke(this, "Scan Bluetooth terminé.");
                }
            });
        }

        private async Task<bool> RequestBluetoothPermissions()
        {
            var bluetoothPermissionStatus = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
            if (bluetoothPermissionStatus != PermissionStatus.Granted)
            {
                bluetoothPermissionStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
            }

            // Location is only required for scanning on older Android versions, but good practice.
            var locationPermissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (locationPermissionStatus != PermissionStatus.Granted)
            {
                locationPermissionStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            return bluetoothPermissionStatus == PermissionStatus.Granted && locationPermissionStatus == PermissionStatus.Granted;
        }

        private void OnDeviceDiscovered(object sender, DeviceEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Device.Name) && _foundDevices.TryAdd(e.Device.Id, e.Device))
            {
                 MainThread.BeginInvokeOnMainThread(() => PeersFound?.Invoke(this, _foundDevices.Values.Select(d => d.Name)));
            }
        }

        private async Task ConnectToPeer(string peerIdentifier)
        {
            try
            {
                _hubDevice = _foundDevices.Values.FirstOrDefault(d => d.Name == peerIdentifier);
                if (_hubDevice == null)
                {
                    StatusChanged?.Invoke(this, $"Appareil {peerIdentifier} non trouvé dans les résultats du scan.");
                    return;
                }

                _adapter.StopScanningForDevicesAsync();
                await _adapter.ConnectToDeviceAsync(_hubDevice);
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, $"Échec de la connexion au Hub BT {peerIdentifier}: {ex.Message}"));
                _hubDevice = null;
            }
        }
        
        public async Task SendMessage(string messageContent)
        {
            var chatMessage = new ChatMessage { SenderId = _deviceId, Content = messageContent };
            var json = JsonConvert.SerializeObject(chatMessage);
            var bytes = Encoding.UTF8.GetBytes(json);

            if (_currentRole == Role.Server)
            {
                await _gattServer.SendMessageToSubscribers(bytes);
                // Also display message on Hub's screen
                MessageReceived?.Invoke(this, $"{chatMessage.SenderId} (Hub): {chatMessage.Content}");
            }
            else if (_hubDevice != null && _hubDevice.State == DeviceState.Connected && _chatCharacteristic != null && _chatCharacteristic.CanWrite)
            {
                await _chatCharacteristic.WriteAsync(bytes);
            }
            else
            {
                StatusChanged?.Invoke(this, "Non connecté pour envoyer un message.");
            }
        }

        public async Task Stop()
        {
            _scanCancellationTokenSource?.Cancel();
            await _adapter.StopScanningForDevicesAsync();
            if (_hubDevice != null)
            {
                try
                {
                    if (_chatCharacteristic != null)
                    {
                        _chatCharacteristic.ValueUpdated -= OnCharacteristicValueUpdated;
                        await _chatCharacteristic.StopUpdatesAsync();
                    }
                    await _adapter.DisconnectDeviceAsync(_hubDevice);
                }
                catch {}
            }
            await _gattServer.Stop();
            _hubDevice = null;
            _chatCharacteristic = null;
            _foundDevices.Clear();
            StatusChanged?.Invoke(this, "Service Bluetooth arrêté.");
        }
    }
}