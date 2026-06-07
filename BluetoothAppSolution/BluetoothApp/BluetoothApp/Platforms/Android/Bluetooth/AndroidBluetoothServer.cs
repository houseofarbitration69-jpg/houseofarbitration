using System.Collections.ObjectModel;
using BluetoothApp.Services;
using Android.Bluetooth;
using System.Text;
using Android.Runtime;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;

namespace BluetoothApp.Platforms.Android.Bluetooth;

public class AndroidBluetoothServer : IBluetoothServer
{
    private BluetoothManager _bluetoothManager;
    private BluetoothAdapter _bluetoothAdapter;
    private BluetoothLeAdvertiser _bluetoothLeAdvertiser;
    private BluetoothGattServer _bluetoothGattServer;
    private AdvertiseCallback _advertiseCallback;
    private BluetoothGattServerCallback _gattServerCallback;

    // A unique UUID for our service
    private readonly Java.Util.UUID ServiceUuid = Java.Util.UUID.FromString("0000180F-0000-1000-8000-00805F9B34FB"); // Example Battery Service UUID
    private readonly Java.Util.UUID CharacteristicUuid = Java.Util.UUID.FromString("00002A19-0000-1000-8000-00805F9B34FB"); // Example Battery Level Characteristic UUID
    private readonly Java.Util.UUID CccdUuid = Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");

    public event EventHandler<string> MessageReceived;
    public event EventHandler<string> DeviceConnected;
    public event EventHandler<string> DeviceDisconnected;

    public ObservableCollection<string> ConnectedClients { get; } = new();
    private List<BluetoothDevice> _connectedDevices = new();
    private HashSet<string> _subscribedDevices = new();

    public AndroidBluetoothServer()
    {
        _bluetoothManager = (BluetoothManager)MauiApplication.Current.GetSystemService(Context.BluetoothService);
        _bluetoothAdapter = _bluetoothManager.Adapter;
    }

    public async Task<bool> StartAdvertising(string serviceUuid, string deviceName)
    {
        if (!_bluetoothAdapter.IsMultipleAdvertisementSupported)
        {
            Console.WriteLine("Advertising not supported on this device.");
            return false;
        }

        // Set the device name if provided
        if (!string.IsNullOrEmpty(deviceName))
        {
            _bluetoothAdapter.SetName(deviceName);
        }

        _bluetoothLeAdvertiser = _bluetoothAdapter.BluetoothLeAdvertiser;
        _advertiseCallback = new CustomAdvertiseCallback(this);

        var settings = new AdvertiseSettings.Builder()
            .SetAdvertiseMode(AdvertiseMode.LowLatency)
            .SetConnectable(true)
            .SetTimeout(0)
            .SetTxPowerLevel(AdvertiseTx.PowerHigh)
            .Build();

        var data = new AdvertiseData.Builder()
            .AddServiceUuid(new ParcelUuid(ServiceUuid))
            .SetIncludeDeviceName(true)
            .Build();

        _bluetoothLeAdvertiser.StartAdvertising(settings, data, _advertiseCallback);

        _gattServerCallback = new CustomGattServerCallback(this);
        _bluetoothGattServer = _bluetoothManager.OpenGattServer(MauiApplication.Current, _gattServerCallback);
        
        var service = new BluetoothGattService(ServiceUuid, GattServiceType.Primary);
        var characteristic = new BluetoothGattCharacteristic(CharacteristicUuid, 
            GattProperty.Read | GattProperty.Write | GattProperty.Notify, 
            GattPermission.Read | GattPermission.Write);
        
        var cccDescriptor = new BluetoothGattDescriptor(CccdUuid, GattDescriptorPermission.Read | GattDescriptorPermission.Write);
        characteristic.AddDescriptor(cccDescriptor);

        service.AddCharacteristic(characteristic);
        _bluetoothGattServer.AddService(service);

        Console.WriteLine("Bluetooth LE Server started advertising.");
        return true;
    }

    public async Task StopAdvertising()
    {
        _bluetoothLeAdvertiser?.StopAdvertising(_advertiseCallback);
        _bluetoothGattServer?.Close();
        Console.WriteLine("Bluetooth LE Server stopped advertising.");
    }

    public async Task SendMessage(string message)
    {
        await SendToAllAsync(message);
    }

    public async Task SendToAllAsync(string message)
    {
        Console.WriteLine($"Attempting to send to all: '{message}'");
        if (_connectedDevices.Any())
        {
            var characteristic = _bluetoothGattServer.GetService(ServiceUuid).GetCharacteristic(CharacteristicUuid);
            characteristic.SetValue(Encoding.UTF8.GetBytes(message));

            Console.WriteLine($"Sending to {_connectedDevices.Count} connected devices.");
            foreach (var device in _connectedDevices)
            {
                if (_subscribedDevices.Contains(device.Address))
                {
                    Console.WriteLine($"Sending notification to subscribed device: {device.Address}");
                    _bluetoothGattServer.NotifyCharacteristicChanged(device, characteristic, false);
                }
                else
                {
                    Console.WriteLine($"Device not subscribed: {device.Address}");
                }
            }
        }
        else
        {
            Console.WriteLine("No connected devices.");
        }
    }

    public async Task SendToClientAsync(string message, string clientId)
    {
        Console.WriteLine($"Attempting to send to client '{clientId}': '{message}'");
        var device = _connectedDevices.FirstOrDefault(d => d.Address == clientId);
        if (device != null)
        {
            if (_subscribedDevices.Contains(device.Address))
            {
                var characteristic = _bluetoothGattServer.GetService(ServiceUuid).GetCharacteristic(CharacteristicUuid);
                characteristic.SetValue(Encoding.UTF8.GetBytes(message));
                Console.WriteLine($"Sending notification to subscribed device: {device.Address}");
                _bluetoothGattServer.NotifyCharacteristicChanged(device, characteristic, false);
            }
            else
            {
                Console.WriteLine($"Device not subscribed: {device.Address}");
            }
        }
        else
        {
            Console.WriteLine($"Device not found: {clientId}");
        }
    }

    // Custom AdvertiseCallback to handle advertising events
    private class CustomAdvertiseCallback : AdvertiseCallback
    {
        private readonly AndroidBluetoothServer _parent;

        public CustomAdvertiseCallback(AndroidBluetoothServer parent)
        {
            _parent = parent;
        }
        
        public override void OnStartSuccess(AdvertiseSettings settingsInEffect)
        {
            base.OnStartSuccess(settingsInEffect);
            Console.WriteLine("Advertisement start succeeded.");
        }

        public override void OnStartFailure([GeneratedEnum] AdvertiseFailure errorCode)
        {
            base.OnStartFailure(errorCode);
            Console.WriteLine($"Advertisement start failed: {errorCode}");
        }
    }

    // Custom GattServerCallback to handle GATT server events
    private class CustomGattServerCallback : BluetoothGattServerCallback
    {
        private readonly AndroidBluetoothServer _parent;

        public CustomGattServerCallback(AndroidBluetoothServer parent)
        {
            _parent = parent;
        }

        public override void OnConnectionStateChange(BluetoothDevice? device, [GeneratedEnum] ProfileState status, [GeneratedEnum] ProfileState newState)
        {
            base.OnConnectionStateChange(device, status, newState);
            if (newState == ProfileState.Connected)
            {
                Console.WriteLine($"Device connected: {device?.Address}");
                _parent.DeviceConnected?.Invoke(_parent, device?.Address ?? String.Empty);
                if (device != null)
                {
                    _parent._connectedDevices.Add(device);
                    _parent.ConnectedClients.Add(device.Address);
                }
            }
            else if (newState == ProfileState.Disconnected)
            {
                Console.WriteLine($"Device disconnected: {device?.Address}");
                _parent.DeviceDisconnected?.Invoke(_parent, device?.Address ?? String.Empty);
                if (device != null)
                {
                    _parent._connectedDevices.Remove(device);
                    _parent.ConnectedClients.Remove(device.Address);
                    _parent._subscribedDevices.Remove(device.Address);
                }
            }
        }

        public override void OnCharacteristicReadRequest(BluetoothDevice device, int requestId, int offset, BluetoothGattCharacteristic characteristic)
        {
            base.OnCharacteristicReadRequest(device, requestId, offset, characteristic);
            Console.WriteLine($"Characteristic read request from {device.Address}");
            if (characteristic.Uuid.Equals(_parent.CharacteristicUuid))
            {
                // Respond with some data, e.g., current time
                var value = Encoding.UTF8.GetBytes(DateTime.Now.ToLongTimeString());
                _parent._bluetoothGattServer.SendResponse(device, requestId, GattStatus.Success, offset, value);
            }
            else
            {
                _parent._bluetoothGattServer.SendResponse(device, requestId, GattStatus.Failure, offset, null);
            }
        }

        public override void OnCharacteristicWriteRequest(BluetoothDevice device, int requestId, BluetoothGattCharacteristic characteristic, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
        {
            base.OnCharacteristicWriteRequest(device, requestId, characteristic, preparedWrite, responseNeeded, offset, value);
            Console.WriteLine($"Characteristic write request from {device.Address}: {Encoding.UTF8.GetString(value)}");
            if (characteristic.Uuid.Equals(_parent.CharacteristicUuid))
            {
                var message = Encoding.UTF8.GetString(value);
                _parent.MessageReceived?.Invoke(_parent, message);
                if (responseNeeded)
                {
                    _parent._bluetoothGattServer.SendResponse(device, requestId, GattStatus.Success, offset, null);
                }
            }
            else
            {
                if (responseNeeded)
                {
                    _parent._bluetoothGattServer.SendResponse(device, requestId, GattStatus.Failure, offset, null);
                }
            }
        }

        public override void OnDescriptorWriteRequest(BluetoothDevice device, int requestId, BluetoothGattDescriptor descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[] value)
        {
            base.OnDescriptorWriteRequest(device, requestId, descriptor, preparedWrite, responseNeeded, offset, value);
            Console.WriteLine($"OnDescriptorWriteRequest from {device.Address}");

            if (descriptor.Uuid.Equals(_parent.CccdUuid))
            {
                if (value.Length == 2 && value[0] == 1 && value[1] == 0) // Enable notification
                {
                    Console.WriteLine($"Enabling notifications for {device.Address}");
                    _parent._subscribedDevices.Add(device.Address);
                }
                else if (value.Length == 2 && value[0] == 0 && value[1] == 0) // Disable notification
                {
                    Console.WriteLine($"Disabling notifications for {device.Address}");
                    _parent._subscribedDevices.Remove(device.Address);
                }

                if (responseNeeded)
                {
                    _parent._bluetoothGattServer.SendResponse(device, requestId, GattStatus.Success, offset, value);
                }
            }
            else
            {
                Console.WriteLine($"Descriptor UUID not recognized: {descriptor.Uuid}");
                if (responseNeeded)
                {
                    _parent._bluetoothGattServer.SendResponse(device, requestId, GattStatus.Failure, offset, null);
                }
            }
        }
    }
}