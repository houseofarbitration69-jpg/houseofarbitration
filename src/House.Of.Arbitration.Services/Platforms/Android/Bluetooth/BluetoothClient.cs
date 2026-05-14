#region Imports
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using ScanMode = Android.Bluetooth.LE.ScanMode;
using House.Of.Arbitration.Services.Abstractions;
using System.Text;
using Android.Runtime;
#endregion

namespace House.Of.Arbitration.Services.Platforms.Android.Bluetooth;

public class BluetoothClient : IBluetoothClient
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    #region Services
    private readonly IAlertService _alertService;
    #endregion

    #region Events
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<(string DeviceId, string Name, int Rssi)>? DeviceDiscovered;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
    #endregion

    #region Constants
    private readonly Java.Util.UUID? ServiceUuid = Java.Util.UUID.FromString("0000180F-0000-1000-8000-00805F9B34FB"); // Example Battery Service UUID
    private readonly Java.Util.UUID? CharacteristicUuid = Java.Util.UUID.FromString("00002A19-0000-1000-8000-00805F9B34FB"); // Example Battery Level Characteristic UUID
    private readonly Java.Util.UUID? CccdUuid = Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");
    #endregion

    #region Attributs
    private BluetoothAdapter? _bluetoothAdapter;
    private BluetoothLeScanner? _bluetoothLeScanner;
    private ScanCallback? _scanCallback;
    private Dictionary<string, BluetoothDevice> _discoveredDevices = new Dictionary<string, BluetoothDevice>();
    private BluetoothGatt? _bluetoothGatt;
    private CustomGattClientCallback? _gattClientCallback;
    private readonly BluetoothTransferManager _transferManager = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    #endregion

    #region Constructors
    public BluetoothClient(IAlertService alertService)
    {
        _alertService = alertService;

        var bluetoothManager = (BluetoothManager?)MauiApplication.Current.GetSystemService(Context.BluetoothService);

        _bluetoothAdapter = bluetoothManager?.Adapter;
        _bluetoothLeScanner = _bluetoothAdapter?.BluetoothLeScanner;

        _scanCallback = new CustomScanCallback(this, _alertService);
    }
    #endregion

    #region Public Methods
    public async Task StartScan()
    {
        _discoveredDevices.Clear();

        var filter = new ScanFilter.Builder()
            ?.SetServiceUuid(new ParcelUuid(ServiceUuid))
            ?.Build();

        var settings = new ScanSettings.Builder()
            ?.SetScanMode(ScanMode.LowLatency)
            ?.Build();

        if (filter != null)
        {
            _bluetoothLeScanner?.StartScan(new List<ScanFilter> { filter }, settings, _scanCallback);
        }

        //await _alertService.ShowToast("Bluetooth LE Client started scanning.");
    }

    public async Task StopScan()
    {
        _bluetoothLeScanner?.StopScan(_scanCallback);

        //await _alertService.ShowToast("Bluetooth LE Client stopped scanning.");
    }

    public async Task ConnectToDevice(string deviceId)
    {
        if (_discoveredDevices != null && _discoveredDevices.TryGetValue(deviceId, out var device))
        {
            _gattClientCallback = new CustomGattClientCallback(this, _alertService);

            if (device != null)
            {
                _bluetoothGatt = device.ConnectGatt(MauiApplication.Current, false, _gattClientCallback, BluetoothTransports.Le);
            }
        }
        else
        {
            await _alertService.ShowToast($"Device {deviceId} not found");
        }
    }

    public async Task DisconnectFromDevice(string deviceId)
    {
        _bluetoothGatt?.Disconnect();
        _transferManager.Clear();
        //await _alertService.ShowToast($"Disconnected from {deviceId}");
    }

    public async Task SendMessage(string deviceId, string message)
    {
        if (_bluetoothGatt == null)
        {
            await _alertService.ShowToast("Not connected to any device.");
            return;
        }

        var service = _bluetoothGatt.GetService(ServiceUuid);
        if (service == null)
        {
            await _alertService.ShowToast("Service not found");
            return;
        }

        var characteristic = service.GetCharacteristic(CharacteristicUuid);
        if (characteristic == null)
        {
            await _alertService.ShowToast("Characteristic not found.");
            return;
        }

        await _sendSemaphore.WaitAsync();
        try
        {
            foreach (var chunk in _transferManager.PrepareMessagesForSending(message))
            {
                characteristic?.SetValue(Encoding.UTF8.GetBytes(chunk ?? String.Empty));
                _bluetoothGatt.WriteCharacteristic(characteristic);

                if (chunk != null && chunk.Length > 0)
                    await Task.Delay(30);
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }

        //await _alertService.ShowToast($"Client sent : {message.Substring(0, Math.Min(20, message.Length))}...");
    }
    #endregion

    #region Private Class
    private class CustomScanCallback : ScanCallback
    {
        #region Services
        private readonly IAlertService _alertService;
        #endregion

        #region Attributs
        private readonly BluetoothClient _parent;
        #endregion

        #region Constructors
        public CustomScanCallback(BluetoothClient parent, IAlertService alertService)
        {
            _parent = parent;
            _alertService = alertService;
        }
        #endregion

        #region Override Methods
        public override async void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult? result)
        {
            base.OnScanResult(callbackType, result);

            if (result != null && result.Device != null && !_parent._discoveredDevices.ContainsKey(result.Device?.Address ?? String.Empty))
            {
                _parent._discoveredDevices.Add(result.Device!.Address ?? String.Empty, result.Device);

                _parent.DeviceDiscovered?.Invoke(_parent, (result.Device.Address ?? String.Empty, result.Device.Name ?? "Unknown", result.Rssi));

                //await _alertService.ShowToast($"Discovered device:{result.Device.Name ?? "Unknown"} ({result.Device.Address}) - RSSI: {result.Rssi}");
            }
        }

        public override void OnBatchScanResults(IList<ScanResult>? results)
        {
            base.OnBatchScanResults(results);

            foreach (var result in results)
            {
                OnScanResult(ScanCallbackType.AllMatchesAutoBatch, result);
            }
        }

        public override async void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
        {
            base.OnScanFailed(errorCode);

            await _alertService.ShowToast($"Scan failed : {errorCode}");
        }
        #endregion
    }

    private class CustomGattClientCallback : BluetoothGattCallback
    {
        #region Services
        private readonly IAlertService _alertService;
        #endregion

        #region Attributs
        private readonly BluetoothClient _parent;
        #endregion

        #region Constructors
        public CustomGattClientCallback(BluetoothClient parent, IAlertService alertService)
        {
            _parent = parent;
            _alertService = alertService;
        }
        #endregion

        #region Override Methods
        public override async void OnConnectionStateChange(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState)
        {
            base.OnConnectionStateChange(gatt, status, newState);

            if (newState == ProfileState.Connected)
            {
                //await _alertService.ShowToast($"Connected to GATT server : {gatt?.Device?.Address}");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _parent.DeviceConnected?.Invoke(_parent, gatt?.Device?.Address ?? String.Empty);
                });

                // Request larger MTU to avoid truncation of JSON messages
                gatt?.RequestMtu(512);
            }
            else if (newState == ProfileState.Disconnected)
            {
                //await _alertService.ShowToast($"Disconnected from GATT server : {gatt?.Device?.Address}");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _parent.DeviceDisconnected?.Invoke(_parent, gatt?.Device?.Address ?? String.Empty);
                });
            }
        }

        public override async void OnMtuChanged(BluetoothGatt? gatt, int mtu, [GeneratedEnum] GattStatus status)
        {
            base.OnMtuChanged(gatt, mtu, status);
            
            //await _alertService.ShowToast($"MTU changed to {mtu} (Status: {status})");
            
            // Proceed to service discovery after MTU is established
            gatt?.DiscoverServices();
        }

        public override async void OnServicesDiscovered(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status)
        {
            base.OnServicesDiscovered(gatt, status);

            if (status == GattStatus.Success)
            {
                //await _alertService.ShowToast("Services discovered");

                var service = gatt?.GetService(_parent.ServiceUuid);
                if (service != null)
                {
                    var characteristic = service.GetCharacteristic(_parent.CharacteristicUuid);

                    if (characteristic != null)
                    {
                        gatt?.SetCharacteristicNotification(characteristic, true);

                        var descriptor = characteristic.GetDescriptor(_parent.CccdUuid);

                        if (descriptor != null)
                        {
                            var value = BluetoothGattDescriptor.EnableNotificationValue.ToArray();
                            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                            {
                                gatt?.WriteDescriptor(descriptor, value);
                            }
                            else
                            {
                                descriptor.SetValue(value);
                                gatt?.WriteDescriptor(descriptor);
                            }

                            //await _alertService.ShowToast("Notifications enabled for characteristic.");
                        }
                        else
                        {
                            await _alertService.ShowToast("CCCD descriptor not found.");
                        }
                    }                    else
                    {
                        await _alertService.ShowToast("Characteristic not found.");
                    }
                }
                else
                {
                    await _alertService.ShowToast("Service not found");
                }
            }
            else
            {
                await _alertService.ShowToast($"Service discovery failed : {status}");
            }
        }

        public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value, [GeneratedEnum] GattStatus status)
        {
            base.OnCharacteristicRead(gatt, characteristic, value, status);

            if(status == GattStatus.Success)
            {
                var data = Encoding.UTF8.GetString(value ?? new byte[0]);
                var message = _parent._transferManager.ProcessReceivedData(data);
                
                if (message != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _parent.MessageReceived?.Invoke(_parent, message);
                    });
                }
            }
        }

        public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, [GeneratedEnum] GattStatus status)
        {
            base.OnCharacteristicWrite(gatt, characteristic, status);

            if(status != GattStatus.Success)
            {
                //_alertService.ShowToast($"Characteristic write failed : {status}");
            }
        }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value)
        {
            base.OnCharacteristicChanged(gatt, characteristic, value);

            if (characteristic != null && characteristic.Uuid.Equals(_parent.CharacteristicUuid))
            {
                var data = value != null ? Encoding.UTF8.GetString(value) : string.Empty;
                var message = _parent._transferManager.ProcessReceivedData(data);

                if (message != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _parent.MessageReceived?.Invoke(_parent, message);
                    });
                }
            }
        }

        public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
        {
            base.OnCharacteristicChanged(gatt, characteristic);

            // On Android 13 (API 33) and above, the version with the byte[] value parameter is called.
            // We skip this one to avoid duplicate processing.
            if (OperatingSystem.IsAndroidVersionAtLeast(33)) return;

            if (characteristic != null && characteristic.Uuid.Equals(_parent.CharacteristicUuid))
            {
                var bytes = characteristic.GetValue();
                var data = bytes != null ? Encoding.UTF8.GetString(bytes) : string.Empty;
                var message = _parent._transferManager.ProcessReceivedData(data);

                if (message != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _parent.MessageReceived?.Invoke(_parent, message);
                    });
                }
            }
        }
        #endregion
    }
    #endregion
}
