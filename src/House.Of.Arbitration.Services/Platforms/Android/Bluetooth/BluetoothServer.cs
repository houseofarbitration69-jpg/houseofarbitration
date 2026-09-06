#region Imports
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Android.Runtime;
using House.Of.Arbitration.Services.Abstractions;
using System.Collections.ObjectModel;
using System.Text;
#endregion

namespace House.Of.Arbitration.Services.Platforms.Android.Bluetooth;

public class BluetoothServer : IBluetoothServer
{
    #region Services
    private readonly IAlertService _alertService;
    #endregion

    #region Events
    public event EventHandler<(string ClientId, string Message)>? MessageReceived;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
    #endregion

    public Guid InstanceId { get; } = Guid.NewGuid();

    #region Constants
    private readonly Java.Util.UUID? ServiceUuid = Java.Util.UUID.FromString("0000180F-0000-1000-8000-00805F9B34FB"); // Example Battery Service UUID
    private readonly Java.Util.UUID? CharacteristicUuid = Java.Util.UUID.FromString("00002A19-0000-1000-8000-00805F9B34FB"); // Example Battery Level Characteristic UUID
    private readonly Java.Util.UUID? CccdUuid = Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");
    #endregion

    #region Attributs
    private BluetoothManager? _bluetoothManager;
    private BluetoothAdapter? _bluetoothAdapter;
    private BluetoothLeAdvertiser? _bluetoothLeAdvertiser;
    private BluetoothGattServer? _bluetoothGattServer;
    private AdvertiseCallback? _advertiseCallback;
    private BluetoothGattServerCallback? _gattServerCallback;

    private List<BluetoothDevice> _connectedDevices = new();
    private HashSet<string> _subscribedDevices = new();
    private readonly BluetoothTransferManager _transferManager = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    #endregion

    #region Properties
    public ObservableCollection<string> ConnectedClients { get; } = new();
    #endregion

    #region Constructors
    public BluetoothServer(IAlertService alertService)
    {
        _alertService = alertService;

        _bluetoothManager = (BluetoothManager?)MauiApplication.Current.GetSystemService(Context.BluetoothService);

        _bluetoothAdapter = _bluetoothManager?.Adapter;
    }
    #endregion

    #region Implement IBluetoothServer
    public async Task<bool> StartAdvertising(string serviceUuid, string deviceName)
    {
        if (_bluetoothAdapter == null || !_bluetoothAdapter.IsMultipleAdvertisementSupported)
        {
            await _alertService.ShowToast("Advertising not supported on this device");

            return false;
        }

        if(!String.IsNullOrEmpty(deviceName))
        {
            _bluetoothAdapter.SetName(deviceName);
        }

        _bluetoothLeAdvertiser = _bluetoothAdapter.BluetoothLeAdvertiser;
        _advertiseCallback = new CustomAdvertiseCallback(this, _alertService);

        var settings = new AdvertiseSettings.Builder()
            ?.SetAdvertiseMode(AdvertiseMode.LowLatency)
            ?.SetConnectable(true)
            ?.SetTimeout(0)
            ?.SetTxPowerLevel(AdvertiseTx.PowerHigh)
            ?.Build();

        var data = new AdvertiseData.Builder()
            ?.AddServiceUuid(new ParcelUuid(ServiceUuid))
            ?.SetIncludeDeviceName(true)
            ?.Build();

        _bluetoothLeAdvertiser?.StartAdvertising(settings, data, _advertiseCallback);

        _gattServerCallback = new CustomGattServerCallback(this, _alertService);
        _bluetoothGattServer = _bluetoothManager?.OpenGattServer(MauiApplication.Current, _gattServerCallback);

        var service = new BluetoothGattService(ServiceUuid, GattServiceType.Primary);

        var characteristic = new BluetoothGattCharacteristic(CharacteristicUuid, GattProperty.Read | GattProperty.Write | GattProperty.Notify, GattPermission.Read | GattPermission.Write);

        var cccDescriptor = new BluetoothGattDescriptor(CccdUuid, GattDescriptorPermission.Read | GattDescriptorPermission.Write);
        characteristic.AddDescriptor(cccDescriptor);

        service.AddCharacteristic(characteristic);
        _bluetoothGattServer?.AddService(service);

        //await _alertService.ShowToast("Bluetooth LE Server started advertising");

        return true;
    }

    public async Task StopAdvertising()
    {
        _bluetoothLeAdvertiser?.StopAdvertising(_advertiseCallback);
        _bluetoothGattServer?.Close();

        //await _alertService.ShowToast("Bluetooth LE Server stopped advertising");
    }

    public async Task SendMessage(string message)
    {
        await SendToAllAsync(message);
    }

    public async Task SendToAllAsync(string message)
    {
        await _sendSemaphore.WaitAsync();
        try
        {
            if (_connectedDevices.Any())
            {
                var characteristic = _bluetoothGattServer?.GetService(ServiceUuid)?.GetCharacteristic(CharacteristicUuid);
                if (characteristic == null) return;

                foreach (var chunk in _transferManager.PrepareMessagesForSending(message))
                {
#pragma warning disable CA1422
                    characteristic.SetValue(Encoding.UTF8.GetBytes(chunk));
#pragma warning restore CA1422

                    foreach (var device in _connectedDevices)
                    {
                        if (device != null && device.Address != null && _subscribedDevices.Contains(device.Address))
                        {
#pragma warning disable CA1422
                            _bluetoothGattServer?.NotifyCharacteristicChanged(device, characteristic, false);
#pragma warning restore CA1422
                        }
                    }

                    if (chunk.Length > 0)
                        await Task.Delay(30);
                }
            }
            else
            {
                await _alertService.ShowToast("No connected devices.");
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public async Task SendToClientAsync(string message, string clientId)
    {
        await _sendSemaphore.WaitAsync();
        try
        {
            var device = _connectedDevices.FirstOrDefault(d => d.Address == clientId);

            if (device != null && device.Address != null)
            {
                if (_subscribedDevices.Contains(device.Address))
                {
                    var characteristic = _bluetoothGattServer?.GetService(ServiceUuid)?.GetCharacteristic(CharacteristicUuid);
                    if (characteristic == null) return;

                    foreach (var chunk in _transferManager.PrepareMessagesForSending(message))
                    {
#pragma warning disable CA1422
                        characteristic.SetValue(Encoding.UTF8.GetBytes(chunk));
                        _bluetoothGattServer?.NotifyCharacteristicChanged(device, characteristic, false);
#pragma warning restore CA1422

                        if (chunk.Length > 0)
                            await Task.Delay(30);
                    }
                }
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }
    #endregion

    #region Private Class
    private class CustomAdvertiseCallback : AdvertiseCallback
    {
        #region Services
        private readonly IAlertService _alertService;
        #endregion

        #region Attributs
        private readonly BluetoothServer _parent;
        #endregion

        #region Constructors
        public CustomAdvertiseCallback(BluetoothServer parent, IAlertService alertService)
        {
            _parent = parent;

            _alertService = alertService;
        }
        #endregion

        #region Override Methods
        public override async void OnStartSuccess(AdvertiseSettings? settingsInEffect)
        {
            base.OnStartSuccess(settingsInEffect);

            //await _alertService.ShowToast("Advertisement start succeeded.");
        }

        public override async void OnStartFailure([GeneratedEnum] AdvertiseFailure errorCode)
        {
            base.OnStartFailure(errorCode);

            await _alertService.ShowToast($"Advertisement start failed: {errorCode}");
        }
        #endregion
    }

    private class CustomGattServerCallback : BluetoothGattServerCallback
    {
        #region Services
        private readonly IAlertService _alertService;
        #endregion

        #region Attributs
        private readonly BluetoothServer _parent;
        #endregion

        #region Constructors
        public CustomGattServerCallback(BluetoothServer parent, IAlertService alertService)
        {
            _parent = parent;
            _alertService = alertService;
        }
        #endregion

        #region Override Methods
        public override async void OnConnectionStateChange(BluetoothDevice? device, [GeneratedEnum] ProfileState status, [GeneratedEnum] ProfileState newState)
        {
            base.OnConnectionStateChange(device, status, newState);

            if (newState == ProfileState.Connected)
            {
                //await _alertService.ShowToast($"Device connected: {device?.Address}");

                _parent.DeviceConnected?.Invoke(_parent, device?.Address ?? String.Empty);

                if (device != null && device.Address != null)
                {
                    if (!_parent._connectedDevices.Any(d => d.Address == device.Address))
                    {
                        _parent._connectedDevices.Add(device);
                        MainThread.BeginInvokeOnMainThread(() => _parent.ConnectedClients.Add(device.Address!));
                    }
                }
            }
            else if (newState == ProfileState.Disconnected)
            {
                //await _alertService.ShowToast($"Device disconnected: {device?.Address ?? String.Empty}");

                _parent.DeviceDisconnected?.Invoke(_parent, device?.Address ?? String.Empty);

                if (device != null)
                {
                    _parent._connectedDevices.Remove(device);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _parent.ConnectedClients.Remove(device!.Address ?? String.Empty);
                        _parent._subscribedDevices.Remove(device!.Address ?? String.Empty);
                    });
                }
            }
        }

        public override async void OnCharacteristicReadRequest(BluetoothDevice? device, int requestId, int offset, BluetoothGattCharacteristic? characteristic)
        {
            base.OnCharacteristicReadRequest(device, requestId, offset, characteristic);

            //await _alertService.ShowToast($"Characteristic read request from {device?.Address}");

            if (characteristic != null && characteristic?.Uuid != null && characteristic.Uuid.Equals(_parent.CharacteristicUuid))
            {
                var value = Encoding.UTF8.GetBytes(DateTime.Now.ToLongTimeString());

                if (device != null)
                {
                    _parent._bluetoothGattServer?.SendResponse(device, requestId, GattStatus.Success, offset, value);
                }
            }
            else
            {
                if (device != null)
                {
                    _parent._bluetoothGattServer?.SendResponse(device, requestId, GattStatus.Failure, offset, new byte[0]);
                }
            }
        }

        public override async void OnCharacteristicWriteRequest(BluetoothDevice? device, int requestId, BluetoothGattCharacteristic? characteristic, bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
        {
            base.OnCharacteristicWriteRequest(device, requestId, characteristic, preparedWrite, responseNeeded, offset, value);

            if (characteristic != null && characteristic.Uuid != null && characteristic.Uuid.Equals(_parent.CharacteristicUuid))
            {
                var data = Encoding.UTF8.GetString(value ?? new byte[0]);
                var message = _parent._transferManager.ProcessReceivedData(data);
                
                if (message != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _parent.MessageReceived?.Invoke(_parent, (device?.Address ?? string.Empty, message));
                    });
                }

                if (responseNeeded)
                {
                    if (device != null)
                    {
                        _parent._bluetoothGattServer?.SendResponse(device, requestId, GattStatus.Success, offset, new byte[0]);
                    }
                }
            }
            else
            {
                if (responseNeeded)
                {
                    if (device != null)
                    {
                        _parent._bluetoothGattServer?.SendResponse(device, requestId, GattStatus.Failure, offset, new byte[0]);
                    }
                }
            }
        }

        public override async void OnDescriptorWriteRequest(BluetoothDevice? device, int requestId, BluetoothGattDescriptor? descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
        {
            base.OnDescriptorWriteRequest(device, requestId, descriptor, preparedWrite, responseNeeded, offset, value);

            //await _alertService.ShowToast($"OnDescriptorWriteRequest from {device?.Address}");

            if (descriptor != null && descriptor.Uuid != null && descriptor.Uuid.Equals(_parent.CccdUuid))
            {
                if (value != null && value.Length == 2 && value[0] == 1 && value[1] == 0)
                {
                    //await _alertService.ShowToast($"Enabling notifications for {device?.Address}");

                    _parent._subscribedDevices.Add(device?.Address ?? String.Empty);
                    _parent.DeviceConnected?.Invoke(_parent, device?.Address ?? String.Empty);
                }
                else if (value != null && value.Length == 2 && value[0] == 0 && value[1] == 0)
                {
                    //await _alertService.ShowToast($"Disabling notifications for {device?.Address}");

                    _parent._subscribedDevices.Remove(device?.Address ?? String.Empty);
                }

                if (responseNeeded)
                {
                    if (device != null)
                    {
                        _parent?._bluetoothGattServer?.SendResponse(device, requestId, GattStatus.Success, offset, value ?? new byte[0]);
                    }
                }
            }
            else
            {
                //await _alertService.ShowToast($"Descriptor UUID not recognized:{descriptor?.Uuid}");

                if(responseNeeded)
                {
                    if(device != null)
                    {
                        _parent._bluetoothGattServer?.SendResponse(device, requestId, GattStatus.Failure, offset, new byte[0]);
                    }
                }
            }
        }
        #endregion
    }
    #endregion
}
