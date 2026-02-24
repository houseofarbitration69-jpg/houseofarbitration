using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Plugin.BLE.Abstractions.Utils;
using System.Text;
using Java.Util; // Added this using directive

namespace TabletTalk.Services
{
    // Indique au compilateur de n'inclure ce fichier que pour la compilation Android.
    public class GattServer : IGattServer
    {
        public event EventHandler<byte[]> MessageReceived;
        public event EventHandler<string> StatusChanged;

        private readonly BluetoothManager _bluetoothManager;
        private BluetoothAdapter _bluetoothAdapter;
        private BluetoothLeAdvertiser _advertiser;
        private BluetoothGattServer _gattServer;

        private MyAdvertiseCallback _advertiseCallback; // Separate callback for advertising
        private MyGattServerCallback _gattServerCallback; // Separate callback for GATT server events

        private BluetoothGattCharacteristic _chatCharacteristic;
        private readonly List<BluetoothDevice> _subscribedDevices = new List<BluetoothDevice>();

        public GattServer()
        {
            var context = Android.App.Application.Context;
            _bluetoothManager = (BluetoothManager)context.GetSystemService(Context.BluetoothService);
            _bluetoothAdapter = _bluetoothManager.Adapter;
        }

        public Task Start(Guid serviceUuid, Guid characteristicUuid)
        {
            StatusChanged?.Invoke(this, "Démarrage du serveur GATT natif Android...");
            if (_bluetoothAdapter == null || !_bluetoothAdapter.IsEnabled)
            {
                StatusChanged?.Invoke(this, "Adaptateur Bluetooth non disponible ou désactivé.");
                return Task.CompletedTask;
            }

            _advertiser = _bluetoothAdapter.BluetoothLeAdvertiser;
            if (_advertiser == null)
            {
                StatusChanged?.Invoke(this, "Publicité LE non supportée sur cet appareil.");
                return Task.CompletedTask;
            }

            // Initialize the GATT Server Callback
            _gattServerCallback = new MyGattServerCallback(this);
            _gattServer = _bluetoothManager.OpenGattServer(Android.App.Application.Context, _gattServerCallback);

            var service = new BluetoothGattService(UUID.FromString(serviceUuid.ToString()), GattServiceType.Primary);
            _chatCharacteristic = new BluetoothGattCharacteristic(
                UUID.FromString(characteristicUuid.ToString()),
                GattProperty.Write | GattProperty.Notify,
                GattPermission.Write);
                
            // Ajouter le descripteur CCCD (Client Characteristic Configuration Descriptor)
            var cccd = new BluetoothGattDescriptor(UUID.FromString("00002902-0000-1000-8000-00805f9b34fb"), GattDescriptorPermission.Write | GattDescriptorPermission.Read);
            _chatCharacteristic.AddDescriptor(cccd);

            service.AddCharacteristic(_chatCharacteristic);
            _gattServer.AddService(service);

            // Initialize and start Advertising
            _advertiseCallback = new MyAdvertiseCallback(this);
            var advertiseSettings = new AdvertiseSettings.Builder()
                .SetAdvertiseMode(AdvertiseMode.Balanced)
                .SetTxPowerLevel(AdvertiseTx.PowerMedium)
                .SetConnectable(true)
                .Build();

            var advertiseData = new AdvertiseData.Builder()
                .SetIncludeDeviceName(true)
                .AddServiceUuid(ParcelUuid.FromString(serviceUuid.ToString()))
                .Build();

            _advertiser.StartAdvertising(advertiseSettings, advertiseData, _advertiseCallback);
            StatusChanged?.Invoke(this, "Serveur GATT démarré et publicité lancée.");
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            _advertiser?.StopAdvertising(_advertiseCallback); // Use advertise callback here
            _gattServer?.Close();
            _gattServer = null;
            _advertiseCallback = null;
            _gattServerCallback = null;
            _subscribedDevices.Clear();
            StatusChanged?.Invoke(this, "Serveur GATT arrêté.");
            return Task.CompletedTask;
        }

        public Task SendMessageToSubscribers(byte[] message)
        {
            _chatCharacteristic.SetValue(message);
            foreach (var device in _subscribedDevices)
            {
                // Ensure device is still connected and can receive notifications
                if (_gattServer != null && _bluetoothManager.GetConnectionState(device, ProfileType.Gatt) == ProfileState.Connected)
                {
                    _gattServer.NotifyCharacteristicChanged(device, _chatCharacteristic, false);
                }
            }
            return Task.CompletedTask;
        }

        // --- Inner callback classes ---

        // Callback pour gérer les événements de publicité
        private class MyAdvertiseCallback : AdvertiseCallback
        {
            private readonly GattServer _server;
            public MyAdvertiseCallback(GattServer server) { _server = server; }

            public override void OnStartSuccess(AdvertiseSettings settingsInEffect)
            {
                _server.StatusChanged?.Invoke(this, "Publicité démarrée avec succès.");
                base.OnStartSuccess(settingsInEffect);
            }

            public override void OnStartFailure(int errorCode)
            {
                _server.StatusChanged?.Invoke(this, $"Échec du démarrage de la publicité: {errorCode}");
                base.OnStartFailure((Android.Bluetooth.LE.AdvertiseFailure)errorCode);
            }
        }

        // Callback pour gérer les événements du serveur GATT
        private class MyGattServerCallback : BluetoothGattServerCallback
        {
            private readonly GattServer _server;
            public MyGattServerCallback(GattServer server) { _server = server; }

            public override void OnConnectionStateChange(BluetoothDevice device, ProfileState status, ProfileState newState)
            {
                base.OnConnectionStateChange(device, status, newState);
                if (newState == ProfileState.Connected)
                {
                    _server.StatusChanged?.Invoke(this, $"Appareil connecté: {device.Name}");
                }
                else if (newState == ProfileState.Disconnected)
                {
                    _server.StatusChanged?.Invoke(this, $"Appareil déconnecté: {device.Name}");
                    lock(_server._subscribedDevices)
                    {
                        _server._subscribedDevices.Remove(device);
                    }
                }
            }
            
            public override void OnCharacteristicWriteRequest(BluetoothDevice device, int requestId, BluetoothGattCharacteristic characteristic, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
            {
                base.OnCharacteristicWriteRequest(device, requestId, characteristic, preparedWrite, responseNeeded, offset, value);
                if (characteristic.Uuid == _server._chatCharacteristic.Uuid)
                {
                    _server.MessageReceived?.Invoke(this, value);
                    if (responseNeeded)
                    {
                        _server._gattServer.SendResponse(device, requestId, GattStatus.Success, offset, value);
                    }
                }
            }

            public override void OnDescriptorWriteRequest(BluetoothDevice device, int requestId, BluetoothGattDescriptor descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
            {
                base.OnDescriptorWriteRequest(device, requestId, descriptor, preparedWrite, responseNeeded, offset, value);
                // The CCCD UUID is 0x2902
                if (descriptor.Uuid.ToString().Equals("00002902-0000-1000-8000-00805f9b34fb"))
                {
                    if (value.SequenceEqual(BluetoothGattDescriptor.EnableNotificationValue.ToArray()))
                    {
                        lock (_server._subscribedDevices)
                        {
                            if (!_server._subscribedDevices.Contains(device))
                            {
                                _server._subscribedDevices.Add(device);
                                _server.StatusChanged?.Invoke(this, $"Appareil {device.Name} abonné aux notifications.");
                            }
                        }
                    }
                    else if (value.SequenceEqual(BluetoothGattDescriptor.DisableNotificationValue.ToArray()))
                    {
                         lock (_server._subscribedDevices)
                        {
                            _server._subscribedDevices.Remove(device);
                            _server.StatusChanged?.Invoke(this, $"Appareil {device.Name} désabonné des notifications.");
                        }
                    }

                    if (responseNeeded)
                    {
                        _server._gattServer.SendResponse(device, requestId, GattStatus.Success, offset, value);
                    }
                }
            }
        }
    }
}
