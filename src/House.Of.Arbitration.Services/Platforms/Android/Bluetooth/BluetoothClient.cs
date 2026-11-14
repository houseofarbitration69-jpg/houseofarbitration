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

/// <summary>
/// Implémentation Android du client Bluetooth LE.
/// Gère le scan, la connexion GATT et l'envoi/réception de messages.
/// </summary>
public class BluetoothClient : IBluetoothClient
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    #region Services
    private readonly IAlertService _alertService;
    #endregion

    #region Events
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<(string DeviceId, string Name, int Rssi)>? DeviceDiscovered;
    /// <summary>Déclenché quand la connexion GATT est pleinement établie (notifications activées).</summary>
    public event EventHandler<string>? DeviceConnected;
    /// <summary>Déclenché quand la connexion GATT est perdue (côté BLE) ou fermée.</summary>
    public event EventHandler<string>? DeviceDisconnected;
    /// <summary>Déclenché lors de la réception d'une mesure de RSSI (puissance du signal).</summary>
    public event EventHandler<int>? RssiUpdated;
    #endregion

    #region UUIDs
    private readonly Java.Util.UUID? ServiceUuid = Java.Util.UUID.FromString("0000180F-0000-1000-8000-00805F9B34FB");
    private readonly Java.Util.UUID? CharacteristicUuid = Java.Util.UUID.FromString("00002A19-0000-1000-8000-00805F9B34FB");
    private readonly Java.Util.UUID? CccdUuid = Java.Util.UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");
    #endregion

    #region State
    private BluetoothAdapter? _bluetoothAdapter;
    private BluetoothLeScanner? _bluetoothLeScanner;
    private ScanCallback? _scanCallback;
    private readonly Dictionary<string, BluetoothDevice> _discoveredDevices = new();
    private BluetoothGatt? _bluetoothGatt;
    private CustomGattClientCallback? _gattCallback;
    private readonly BluetoothTransferManager _transferManager = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    // Protège ConnectToDevice contre les appels concurrents
    private int _connecting = 0;
    #endregion

    #region Constructor
    public BluetoothClient(IAlertService alertService)
    {
        _alertService = alertService;
        RefreshBluetoothAdapter();
        _scanCallback = new CustomScanCallback(this, _alertService);
    }
    #endregion

    #region Public API

    public async Task StartScan()
    {
        _discoveredDevices.Clear();
        RefreshBluetoothAdapter();

        var filter = new ScanFilter.Builder()
            ?.SetServiceUuid(new ParcelUuid(ServiceUuid))
            ?.Build();

        var settings = new ScanSettings.Builder()
            ?.SetScanMode(ScanMode.LowLatency)
            ?.Build();

        if (filter != null)
            _bluetoothLeScanner?.StartScan(new List<ScanFilter> { filter }, settings, _scanCallback);
    }

    public async Task StopScan()
    {
        try { _bluetoothLeScanner?.StopScan(_scanCallback); } catch { }
    }

    /// <summary>
    /// Lance une connexion GATT vers le périphérique identifié par <paramref name="deviceId"/>.
    /// La connexion est asynchrone : DeviceConnected est déclenché en cas de succès,
    /// DeviceDisconnected en cas d'échec ou de perte de connexion.
    /// </summary>
    public async Task ConnectToDevice(string deviceId)
    {
        // Un seul appel concurrent autorisé
        if (Interlocked.CompareExchange(ref _connecting, 1, 0) != 0)
        {
            await _alertService.ShowToast("ConnectToDevice : déjà en cours, appel ignoré.");
            return;
        }

        try
        {
            // Fermer proprement toute connexion GATT existante
            CloseGatt();

            // Rafraîchir l'adapter (nécessaire après toggle BT)
            RefreshBluetoothAdapter();

            if (_bluetoothAdapter == null || !_bluetoothAdapter.IsEnabled)
            {
                await _alertService.ShowToast("Bluetooth non disponible ou désactivé.");
                MainThread.BeginInvokeOnMainThread(() =>
                    DeviceDisconnected?.Invoke(this, deviceId));
                return;
            }

            // Résoudre le device BLE
            BluetoothDevice? device = null;
            _discoveredDevices.TryGetValue(deviceId, out device);
            if (device == null)
            {
                try { device = _bluetoothAdapter.GetRemoteDevice(deviceId); }
                catch { device = null; }
            }

            if (device == null)
            {
                await _alertService.ShowToast($"Périphérique {deviceId} introuvable.");
                // Notifier l'échec comme une déconnexion pour que le ViewModel puisse réagir
                MainThread.BeginInvokeOnMainThread(() =>
                    DeviceDisconnected?.Invoke(this, deviceId));
                return;
            }

            await _alertService.ShowToast($"ConnectGatt → {deviceId}...");
            _gattCallback = new CustomGattClientCallback(this, _alertService);
            _bluetoothGatt = device.ConnectGatt(MauiApplication.Current, false, _gattCallback, BluetoothTransports.Le);
            await _alertService.ShowToast($"ConnectGatt lancé (gatt={_bluetoothGatt != null})");
        }
        finally
        {
            Interlocked.Exchange(ref _connecting, 0);
        }
    }

    public async Task DisconnectFromDevice(string deviceId)
    {
        CloseGatt();
    }

    public async Task SendMessage(string deviceId, string message)
    {
        if (_bluetoothGatt == null) return;

        var service = _bluetoothGatt.GetService(ServiceUuid);
        if (service == null) return;

        var characteristic = service.GetCharacteristic(CharacteristicUuid);
        if (characteristic == null) return;

        await _sendSemaphore.WaitAsync();
        try
        {
            foreach (var chunk in _transferManager.PrepareMessagesForSending(message))
            {
#pragma warning disable CA1422
                characteristic.SetValue(Encoding.UTF8.GetBytes(chunk ?? string.Empty));
                _bluetoothGatt.WriteCharacteristic(characteristic);
#pragma warning restore CA1422
                if (chunk != null && chunk.Length > 0)
                    await Task.Delay(30);
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    /// <summary>
    /// Demande la lecture asynchrone de la puissance du signal (RSSI) auprès du périphérique connecté.
    /// Le résultat est notifié via l'événement RssiUpdated.
    /// </summary>
    public Task ReadRssiAsync()
    {
        try
        {
            _bluetoothGatt?.ReadRemoteRssi();
        }
        catch { }
        return Task.CompletedTask;
    }

    #endregion

    #region Private helpers

    /// <summary>Ferme et libère le GATT actuel sans notifier.</summary>
    private void CloseGatt()
    {
        var gatt = _bluetoothGatt;
        _bluetoothGatt = null;
        _transferManager.Clear();
        if (gatt != null)
        {
            try { gatt.Disconnect(); } catch { }
            try { gatt.Close(); } catch { }
        }
    }

    /// <summary>Relit le BluetoothAdapter depuis le système Android (utile après toggle BT).</summary>
    private void RefreshBluetoothAdapter()
    {
        try
        {
            var mgr = (BluetoothManager?)MauiApplication.Current.GetSystemService(Context.BluetoothService);
            var adapter = mgr?.Adapter;
            if (adapter != null)
            {
                _bluetoothAdapter = adapter;
                _bluetoothLeScanner = adapter.BluetoothLeScanner;
            }
        }
        catch { }
    }

    #endregion

    #region Scan callback
    private class CustomScanCallback : ScanCallback
    {
        private readonly BluetoothClient _parent;
        private readonly IAlertService _alertService;

        public CustomScanCallback(BluetoothClient parent, IAlertService alertService)
        {
            _parent = parent;
            _alertService = alertService;
        }

        public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult? result)
        {
            base.OnScanResult(callbackType, result);
            if (result?.Device?.Address == null) return;

            var address = result.Device.Address;
            string name = string.Empty;

            var mfgBytes = result.ScanRecord?.GetManufacturerSpecificData(Models.Constants.BLE_MANUFACTURER_ID);
            if (mfgBytes != null && mfgBytes.Length > 0)
            {
                try { name = Encoding.UTF8.GetString(mfgBytes).Trim(); } catch { }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = !string.IsNullOrWhiteSpace(result.ScanRecord?.DeviceName)
                    ? result.ScanRecord.DeviceName
                    : (result.Device.Name ?? "Unknown");
            }

            if (!_parent._discoveredDevices.ContainsKey(address))
                _parent._discoveredDevices[address] = result.Device;

            _parent.DeviceDiscovered?.Invoke(_parent, (address, name, result.Rssi));
        }

        public override void OnBatchScanResults(IList<ScanResult>? results)
        {
            base.OnBatchScanResults(results);
            if (results == null) return;
            foreach (var r in results)
            {
#pragma warning disable CA1416
                OnScanResult(ScanCallbackType.AllMatchesAutoBatch, r);
#pragma warning restore CA1416
            }
        }

        public override async void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
        {
            base.OnScanFailed(errorCode);
            await _alertService.ShowToast($"Scan BLE échoué : {errorCode}");
        }
    }
    #endregion

    #region GATT callback
    private class CustomGattClientCallback : BluetoothGattCallback
    {
        private readonly BluetoothClient _parent;
        private readonly IAlertService _alertService;

        public CustomGattClientCallback(BluetoothClient parent, IAlertService alertService)
        {
            _parent = parent;
            _alertService = alertService;
        }

        public override void OnConnectionStateChange(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState)
        {
            base.OnConnectionStateChange(gatt, status, newState);

            MainThread.BeginInvokeOnMainThread(async () =>
                await _alertService.ShowToast($"BLE State: {newState} (status={status})"));

            if (newState == ProfileState.Connected)
            {
                // Mettre à jour le GATT actif
                if (gatt != null) _parent._bluetoothGatt = gatt;
                // Négociation MTU pour les grands messages JSON
                if (gatt?.RequestMtu(512) != true)
                    gatt?.DiscoverServices();
            }
            else if (newState == ProfileState.Disconnected)
            {
                // Ignorer les callbacks fantômes d'anciens objets GATT
                if (_parent._bluetoothGatt != gatt)
                {
                    try { gatt?.Close(); } catch { }
                    return;
                }

                var address = gatt?.Device?.Address ?? string.Empty;
                _parent._bluetoothGatt = null;
                _parent._transferManager.Clear();
                try { gatt?.Close(); } catch { }

                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.DeviceDisconnected?.Invoke(_parent, address));
            }
        }

        public override void OnMtuChanged(BluetoothGatt? gatt, int mtu, [GeneratedEnum] GattStatus status)
        {
            base.OnMtuChanged(gatt, mtu, status);
            gatt?.DiscoverServices();
        }

        public override async void OnServicesDiscovered(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status)
        {
            base.OnServicesDiscovered(gatt, status);

            // Vérifier que c'est bien le GATT actif
            if (_parent._bluetoothGatt != gatt) return;

            var address = gatt?.Device?.Address ?? string.Empty;

            if (status != GattStatus.Success)
            {
                await _alertService.ShowToast($"Découverte des services échouée : {status}");
                // Fermer et notifier la déconnexion pour permettre une nouvelle tentative
                _parent._bluetoothGatt = null;
                try { gatt?.Disconnect(); gatt?.Close(); } catch { }
                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.DeviceDisconnected?.Invoke(_parent, address));
                return;
            }

            var service = gatt?.GetService(_parent.ServiceUuid);
            if (service == null)
            {
                await _alertService.ShowToast("Service d'arbitrage introuvable.");
                _parent._bluetoothGatt = null;
                try { gatt?.Disconnect(); gatt?.Close(); } catch { }
                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.DeviceDisconnected?.Invoke(_parent, address));
                return;
            }

            var characteristic = service.GetCharacteristic(_parent.CharacteristicUuid);
            if (characteristic == null)
            {
                await _alertService.ShowToast("Caractéristique d'arbitrage introuvable.");
                _parent._bluetoothGatt = null;
                try { gatt?.Disconnect(); gatt?.Close(); } catch { }
                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.DeviceDisconnected?.Invoke(_parent, address));
                return;
            }

            // Activer les notifications GATT
            gatt?.SetCharacteristicNotification(characteristic, true);
            var descriptor = characteristic.GetDescriptor(_parent.CccdUuid);

            if (descriptor != null)
            {
                var value = BluetoothGattDescriptor.EnableNotificationValue?.ToArray();
                if (value != null)
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                    {
#pragma warning disable CA1416
                        gatt?.WriteDescriptor(descriptor, value);
#pragma warning restore CA1416
                        // DeviceConnected sera déclenché par OnDescriptorWrite
                        return;
                    }
                    else
                    {
#pragma warning disable CA1422
                        descriptor.SetValue(value);
                        gatt?.WriteDescriptor(descriptor);
#pragma warning restore CA1422
                        // DeviceConnected sera déclenché par OnDescriptorWrite
                        return;
                    }
                }
            }

            // Pas de CCCD ou valeur nulle : on est quand même connecté
            MainThread.BeginInvokeOnMainThread(() =>
                _parent.DeviceConnected?.Invoke(_parent, address));
        }

        public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, [GeneratedEnum] GattStatus status)
        {
            base.OnDescriptorWrite(gatt, descriptor, status);

            // Ignorer les callbacks d'anciens GATT
            if (_parent._bluetoothGatt != gatt) return;

            var address = gatt?.Device?.Address ?? string.Empty;
            // Connexion pleinement établie (notifications activées)
            MainThread.BeginInvokeOnMainThread(() =>
                _parent.DeviceConnected?.Invoke(_parent, address));
        }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value)
        {
#pragma warning disable CA1416
            base.OnCharacteristicChanged(gatt, characteristic, value);
            if (!characteristic.Uuid.Equals(_parent.CharacteristicUuid)) return;
            var data = value != null ? Encoding.UTF8.GetString(value) : string.Empty;
            var message = _parent._transferManager.ProcessReceivedData(data);
            if (message != null)
                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.MessageReceived?.Invoke(_parent, message));
#pragma warning restore CA1416
        }

        public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
        {
#pragma warning disable CA1422
            base.OnCharacteristicChanged(gatt, characteristic);
            if (OperatingSystem.IsAndroidVersionAtLeast(33)) return; // géré par l'overload byte[]
            if (characteristic == null || !characteristic.Uuid.Equals(_parent.CharacteristicUuid)) return;
            var bytes = characteristic.GetValue();
            var data = bytes != null ? Encoding.UTF8.GetString(bytes) : string.Empty;
            var message = _parent._transferManager.ProcessReceivedData(data);
            if (message != null)
                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.MessageReceived?.Invoke(_parent, message));
#pragma warning restore CA1422
        }

        public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, [GeneratedEnum] GattStatus status)
        {
            base.OnCharacteristicWrite(gatt, characteristic, status);
        }

        public override void OnReadRemoteRssi(BluetoothGatt? gatt, int rssi, [GeneratedEnum] GattStatus status)
        {
            base.OnReadRemoteRssi(gatt, rssi, status);
            if (status == GattStatus.Success && _parent._bluetoothGatt == gatt)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    _parent.RssiUpdated?.Invoke(_parent, rssi));
            }
        }
    }
    #endregion
}
