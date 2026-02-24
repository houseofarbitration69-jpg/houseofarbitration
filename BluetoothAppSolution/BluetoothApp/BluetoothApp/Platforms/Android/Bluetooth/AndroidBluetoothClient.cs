using BluetoothApp.Services;
using Android.Bluetooth;
using Android.Content;
using System.Text;
using Android.Bluetooth.LE;
using Android.OS;
using ScanMode = Android.Bluetooth.LE.ScanMode;

namespace BluetoothApp.Platforms.Android.Bluetooth;

public class AndroidBluetoothClient : IBluetoothClient
{
    private BluetoothAdapter _bluetoothAdapter;
    private BluetoothLeScanner _bluetoothLeScanner;
    private ScanCallback _scanCallback;
    private Dictionary<string, BluetoothDevice> _discoveredDevices = new Dictionary<string, BluetoothDevice>();
    private BluetoothGatt _bluetoothGatt;
    private CustomGattClientCallback _gattClientCallback;

    private readonly Java.Util.UUID ServiceUuid = Java.Util.UUID.FromString("0000180F-0000-1000-8000-00805F9B34FB"); // Example Battery Service UUID
    private readonly Java.Util.UUID CharacteristicUuid = Java.Util.UUID.FromString("00002A19-0000-1000-8000-00805F9B34FB"); // Example Battery Level Characteristic UUID
    private readonly Java.Util.UUID CccdUuid = Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");

    public event EventHandler<string> MessageReceived;
    public event EventHandler<string> DeviceDiscovered;
    public event EventHandler<string> DeviceConnected;
    public event EventHandler<string> DeviceDisconnected;

    public AndroidBluetoothClient()
    {
        var bluetoothManager = (BluetoothManager)MauiApplication.Current.GetSystemService(Context.BluetoothService);
        _bluetoothAdapter = bluetoothManager.Adapter;
        _bluetoothLeScanner = _bluetoothAdapter.BluetoothLeScanner;
        _scanCallback = new CustomScanCallback(this);
    }

    public Task StartScan()
    {
        _discoveredDevices.Clear();
        var filter = new ScanFilter.Builder()
            .SetServiceUuid(new ParcelUuid(ServiceUuid))
            .Build();

        var settings = new ScanSettings.Builder()
            .SetScanMode(ScanMode.LowLatency)
            .Build();

        _bluetoothLeScanner.StartScan(new List<ScanFilter> { filter }, settings, _scanCallback);
        Console.WriteLine("Bluetooth LE Client started scanning.");
        return Task.CompletedTask;
    }

    public Task StopScan()
    {
        _bluetoothLeScanner?.StopScan(_scanCallback);
        Console.WriteLine("Bluetooth LE Client stopped scanning.");
        return Task.CompletedTask;
    }

    public Task ConnectToDevice(string deviceId)
    {
        if (_discoveredDevices.TryGetValue(deviceId, out var device))
        {
            _gattClientCallback = new CustomGattClientCallback(this);
            _bluetoothGatt = device.ConnectGatt(MauiApplication.Current, false, _gattClientCallback, BluetoothTransports.Le);
            Console.WriteLine($"Attempting to connect to {deviceId}");
        }
        else
        {
            Console.WriteLine($"Device {deviceId} not found.");
        }
        return Task.CompletedTask;
    }

    public Task DisconnectFromDevice(string deviceId)
    {
        _bluetoothGatt?.Disconnect();
        Console.WriteLine($"Disconnected from {deviceId}");
        return Task.CompletedTask;
    }

    public async Task SendMessage(string deviceId, string message)
    {
        if (_bluetoothGatt == null)
        {
            Console.WriteLine("Not connected to any device.");
            return;
        }

        var service = _bluetoothGatt.GetService(ServiceUuid);
        if (service == null)
        {
            Console.WriteLine("Service not found.");
            return;
        }

        var characteristic = service.GetCharacteristic(CharacteristicUuid);
        if (characteristic == null)
        {
            Console.WriteLine("Characteristic not found.");
            return;
        }

        characteristic.SetValue(Encoding.UTF8.GetBytes(message));
        _bluetoothGatt.WriteCharacteristic(characteristic);
        Console.WriteLine($"Client sent: {message}");
    }

    // Custom ScanCallback to handle scan results
    private class CustomScanCallback : ScanCallback
    {
        private readonly AndroidBluetoothClient _parent;

        public CustomScanCallback(AndroidBluetoothClient parent)
        {
            _parent = parent;
        }

        public override void OnScanResult(ScanCallbackType callbackType, ScanResult result)
        {
            base.OnScanResult(callbackType, result);
            if (result.Device != null && !_parent._discoveredDevices.ContainsKey(result.Device.Address))
            {
                _parent._discoveredDevices.Add(result.Device.Address, result.Device);
                _parent.DeviceDiscovered?.Invoke(_parent, result.Device.Address);
                Console.WriteLine($"Discovered device: {result.Device.Name ?? "Unknown"} ({result.Device.Address})");
            }
        }

        public override void OnBatchScanResults(IList<ScanResult> results)
        {
            base.OnBatchScanResults(results);
            foreach (var result in results)
            {
                OnScanResult(ScanCallbackType.AllMatchesAutoBatch, result);
            }
        }

        public override void OnScanFailed(ScanFailure errorCode)
        {
            base.OnScanFailed(errorCode);
            Console.WriteLine($"Scan failed: {errorCode}");
        }
    }

    // Custom GattClientCallback to handle GATT client events
    private class CustomGattClientCallback : BluetoothGattCallback
    {
        private readonly AndroidBluetoothClient _parent;

        public CustomGattClientCallback(AndroidBluetoothClient parent)
        {
            _parent = parent;
        }

        public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
        {
            base.OnConnectionStateChange(gatt, status, newState);
            if (newState == ProfileState.Connected)
            {
                Console.WriteLine($"Connected to GATT server: {gatt.Device.Address}");
                _parent.DeviceConnected?.Invoke(_parent, gatt.Device.Address);
                gatt.DiscoverServices();
            }
            else if (newState == ProfileState.Disconnected)
            {
                Console.WriteLine($"Disconnected from GATT server: {gatt.Device.Address}");
                _parent.DeviceDisconnected?.Invoke(_parent, gatt.Device.Address);
            }
        }

        public override void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
        {
            base.OnServicesDiscovered(gatt, status);
            if (status == GattStatus.Success)
            {
                Console.WriteLine("Services discovered.");
                var service = gatt.GetService(_parent.ServiceUuid);
                if (service != null)
                {
                    var characteristic = service.GetCharacteristic(_parent.CharacteristicUuid);
                    if (characteristic != null)
                    {
                        gatt.SetCharacteristicNotification(characteristic, true);
                        var descriptor = characteristic.GetDescriptor(_parent.CccdUuid);
                        if (descriptor != null)
                        {
                            descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
                            gatt.WriteDescriptor(descriptor);
                            Console.WriteLine("Notifications enabled for characteristic.");
                        }
                        else
                        {
                            Console.WriteLine("CCCD descriptor not found.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Characteristic not found.");
                    }
                }
                else
                {
                    Console.WriteLine("Service not found.");
                }
            }
            else
            {
                Console.WriteLine($"Service discovery failed: {status}");
            }
        }

        public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
        {
            base.OnCharacteristicRead(gatt, characteristic, status);
            if (status == GattStatus.Success)
            {
                var message = Encoding.UTF8.GetString(characteristic.GetValue());
                _parent.MessageReceived?.Invoke(_parent, message);
                Console.WriteLine($"Client received: {message}");
            }
        }

        public override void OnCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
        {
            base.OnCharacteristicWrite(gatt, characteristic, status);
            if (status == GattStatus.Success)
            {
                Console.WriteLine("Characteristic write successful.");
            }
            else
            {
                Console.WriteLine($"Characteristic write failed: {status}");
            }
        }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
        {
            base.OnCharacteristicChanged(gatt, characteristic);
            if (characteristic.Uuid.Equals(_parent.CharacteristicUuid))
            {
                var message = Encoding.UTF8.GetString(characteristic.GetValue());
                _parent.MessageReceived?.Invoke(_parent, message);
                Console.WriteLine($"Client received notification: {message}");
            }
        }
    }
}