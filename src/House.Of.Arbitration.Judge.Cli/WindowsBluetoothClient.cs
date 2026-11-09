using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace House.Of.Arbitration.Judge.Cli;

public class DiscoveredDevice
{
    public ulong Address { get; set; }
    public BluetoothAddressType AddressType { get; set; } = BluetoothAddressType.Public;
    public string AddressFormatted { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public short Rssi { get; set; }
    public bool HasArbitrationService { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public override string ToString() =>
        $"[{AddressFormatted}] {Name} (RSSI: {Rssi} dBm, Type: {AddressType})" + (HasArbitrationService ? " [SERVEUR ARBITRATION]" : "");
}

/// <summary>
/// Client Bluetooth Low Energy natif Windows (WinRT) pour communiquer avec le serveur d'arbitrage.
/// </summary>
public class WindowsBluetoothClient : IDisposable
{
    public static readonly Guid ArbitrationServiceUuid = new("0000180F-0000-1000-8000-00805F9B34FB");
    public static readonly Guid ArbitrationCharacteristicUuid = new("00002A19-0000-1000-8000-00805F9B34FB");

    private readonly BluetoothTransferManager _transferManager = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private readonly Dictionary<ulong, DiscoveredDevice> _discoveredDevices = new();

    private BluetoothLEAdvertisementWatcher? _watcher;
    private BluetoothLEDevice? _device;
    private GattSession? _gattSession;
    private GattDeviceService? _service;
    private GattCharacteristic? _characteristic;

    public event EventHandler<DiscoveredDevice>? DeviceDiscovered;
    public event EventHandler<string>? Connected;
    public event EventHandler<string>? Disconnected;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? LogMessage;

    public bool IsConnected => _device != null &&
                               _device.ConnectionStatus == BluetoothConnectionStatus.Connected &&
                               _characteristic != null;

    public string? ConnectedDeviceName => _device?.Name;
    public string? ConnectedDeviceAddress { get; private set; }

    public IReadOnlyList<DiscoveredDevice> DiscoveredDevices
    {
        get
        {
            lock (_discoveredDevices)
            {
                return _discoveredDevices.Values.OrderByDescending(d => d.HasArbitrationService).ThenByDescending(d => d.Rssi).ToList();
            }
        }
    }

    /// <summary>
    /// Scanne les périphériques Bluetooth pendant une durée spécifiée.
    /// </summary>
    public async Task<IReadOnlyList<DiscoveredDevice>> ScanForDevicesAsync(TimeSpan duration)
    {
        StartScan();
        await Task.Delay(duration);
        StopScan();
        return DiscoveredDevices;
    }

    /// <summary>
    /// Démarre le scan des périphériques Bluetooth Low Energy.
    /// </summary>
    public void StartScan()
    {
        StopScan();

        lock (_discoveredDevices)
        {
            _discoveredDevices.Clear();
        }

        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        _watcher.Received += OnAdvertisementReceived;
        _watcher.Stopped += (s, e) => LogMessage?.Invoke(this, $"Scan Bluetooth arrêté (Raison: {e.Error}).");

        _watcher.Start();
        LogMessage?.Invoke(this, "Scan Bluetooth LE démarré...");
    }

    /// <summary>
    /// Arrête le scan des périphériques Bluetooth.
    /// </summary>
    public void StopScan()
    {
        if (_watcher != null)
        {
            try
            {
                _watcher.Received -= OnAdvertisementReceived;
                _watcher.Stop();
            }
            catch { }
            _watcher = null;
            LogMessage?.Invoke(this, "Scan Bluetooth LE arrêté.");
        }
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        ulong address = args.BluetoothAddress;
        BluetoothAddressType addressType = args.BluetoothAddressType;
        string addressStr = FormatAddress(address);
        string name = !string.IsNullOrWhiteSpace(args.Advertisement.LocalName)
            ? args.Advertisement.LocalName
            : "Inconnu";

        bool hasService = args.Advertisement.ServiceUuids.Contains(ArbitrationServiceUuid);

        DiscoveredDevice dev;

        lock (_discoveredDevices)
        {
            if (_discoveredDevices.TryGetValue(address, out var existing))
            {
                dev = existing;
                if (!string.IsNullOrWhiteSpace(name) && name != "Inconnu") dev.Name = name;
                dev.AddressType = addressType;
                dev.Rssi = args.RawSignalStrengthInDBm;
                dev.HasArbitrationService = dev.HasArbitrationService || hasService;
                dev.LastSeen = DateTime.UtcNow;
            }
            else
            {
                dev = new DiscoveredDevice
                {
                    Address = address,
                    AddressType = addressType,
                    AddressFormatted = addressStr,
                    Name = name,
                    Rssi = args.RawSignalStrengthInDBm,
                    HasArbitrationService = hasService,
                    LastSeen = DateTime.UtcNow
                };
                _discoveredDevices[address] = dev;
            }
        }

        DeviceDiscovered?.Invoke(this, dev);
    }

    /// <summary>
    /// Se connecte au périphérique BLE spécifié par un objet DiscoveredDevice.
    /// </summary>
    public async Task<bool> ConnectAsync(DiscoveredDevice device)
    {
        return await ConnectAsync(device.Address, device.AddressType);
    }

    /// <summary>
    /// Se connecte au périphérique BLE spécifié par son adresse Bluetooth (ulong).
    /// </summary>
    public async Task<bool> ConnectAsync(ulong bluetoothAddress, BluetoothAddressType addressType = BluetoothAddressType.Public)
    {
        await DisconnectAsync();

        lock (_discoveredDevices)
        {
            if (_discoveredDevices.TryGetValue(bluetoothAddress, out var dev))
            {
                addressType = dev.AddressType;
            }
        }

        string addrStr = FormatAddress(bluetoothAddress);
        LogMessage?.Invoke(this, $"Connexion au périphérique [{addrStr}] (AddressType: {addressType})...");

        try
        {
            _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress, addressType);
            if (_device == null)
            {
                var fallbackType = addressType == BluetoothAddressType.Public ? BluetoothAddressType.Random : BluetoothAddressType.Public;
                LogMessage?.Invoke(this, $"Type {addressType} non concluant, tentative avec {fallbackType}...");
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress, fallbackType);
            }

            if (_device == null)
            {
                LogMessage?.Invoke(this, $"Échec : Impossible d'obtenir le périphérique Bluetooth [{addrStr}]. Vérifiez que le Bluetooth est activé.");
                return false;
            }

            ConnectedDeviceAddress = addrStr;
            _device.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;

            // Maintien de la connexion physique BLE active via GattSession
            try
            {
                _gattSession = await GattSession.FromDeviceIdAsync(_device.BluetoothDeviceId);
                if (_gattSession != null)
                {
                    _gattSession.MaintainConnection = true;
                    LogMessage?.Invoke(this, "Session GATT active (MaintainConnection = true).");
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Avertissement GattSession : {ex.Message}");
            }

            // Découverte du service GATT d'arbitrage
            LogMessage?.Invoke(this, $"Recherche du service GATT ({ArbitrationServiceUuid})...");
            var servicesResult = await _device.GetGattServicesForUuidAsync(ArbitrationServiceUuid, BluetoothCacheMode.Uncached);

            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                LogMessage?.Invoke(this, $"Service non trouvé via filtre direct ({servicesResult.Status}), analyse complète des services...");
                var allServicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (allServicesResult.Status == GattCommunicationStatus.Success)
                {
                    _service = allServicesResult.Services.FirstOrDefault(s => s.Uuid == ArbitrationServiceUuid);
                }
            }
            else
            {
                _service = servicesResult.Services[0];
            }

            if (_service == null)
            {
                LogMessage?.Invoke(this, $"Échec : Le service d'arbitrage {ArbitrationServiceUuid} n'a pas été trouvé sur ce périphérique.");
                await DisconnectAsync();
                return false;
            }

            // Découverte de la caractéristique GATT d'échange
            LogMessage?.Invoke(this, $"Recherche de la caractéristique GATT ({ArbitrationCharacteristicUuid})...");
            var charResult = await _service.GetCharacteristicsForUuidAsync(ArbitrationCharacteristicUuid, BluetoothCacheMode.Uncached);

            if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
            {
                var allCharsResult = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                if (allCharsResult.Status == GattCommunicationStatus.Success)
                {
                    _characteristic = allCharsResult.Characteristics.FirstOrDefault(c => c.Uuid == ArbitrationCharacteristicUuid);
                }
            }
            else
            {
                _characteristic = charResult.Characteristics[0];
            }

            if (_characteristic == null)
            {
                LogMessage?.Invoke(this, $"Échec : Caractéristique {ArbitrationCharacteristicUuid} introuvable.");
                await DisconnectAsync();
                return false;
            }

            // Inscription aux notifications
            LogMessage?.Invoke(this, "Activation des notifications GATT...");
            var notifyStatus = await _characteristic.WriteClientCharacteristicConfigurationDescriptorWithResultAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (notifyStatus.Status != GattCommunicationStatus.Success)
            {
                LogMessage?.Invoke(this, $"Avertissement : Erreur activation notifications ({notifyStatus.Status}).");
            }

            _characteristic.ValueChanged += OnCharacteristicValueChanged;
            _transferManager.Clear();

            LogMessage?.Invoke(this, $"Connecté avec succès au serveur [{_device.Name ?? addrStr}] !");
            Connected?.Invoke(this, addrStr);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur lors de la connexion Bluetooth : {ex.Message}");
            await DisconnectAsync();
            return false;
        }
    }

    /// <summary>
    /// Se connecte par chaîne d'adresse (ex: "AA:BB:CC:DD:EE:FF" ou "AABBCCDDEEFF").
    /// </summary>
    public async Task<bool> ConnectAsync(string addressString)
    {
        if (TryParseAddress(addressString, out ulong address))
        {
            return await ConnectAsync(address);
        }

        LogMessage?.Invoke(this, $"Format d'adresse Bluetooth invalide : '{addressString}'.");
        return false;
    }

    /// <summary>
    /// Déconnecte le périphérique actuel.
    /// </summary>
    public async Task DisconnectAsync()
    {
        string? addr = ConnectedDeviceAddress;

        if (_gattSession != null)
        {
            try
            {
                _gattSession.MaintainConnection = false;
                _gattSession.Dispose();
            }
            catch { }
            _gattSession = null;
        }

        if (_characteristic != null)
        {
            try
            {
                _characteristic.ValueChanged -= OnCharacteristicValueChanged;
                await _characteristic.WriteClientCharacteristicConfigurationDescriptorWithResultAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch { }
            _characteristic = null;
        }

        if (_service != null)
        {
            try { _service.Dispose(); } catch { }
            _service = null;
        }

        if (_device != null)
        {
            try
            {
                _device.ConnectionStatusChanged -= OnDeviceConnectionStatusChanged;
                _device.Dispose();
            }
            catch { }
            _device = null;
        }

        _transferManager.Clear();
        ConnectedDeviceAddress = null;

        if (!string.IsNullOrEmpty(addr))
        {
            Disconnected?.Invoke(this, addr);
        }
    }

    /// <summary>
    /// Envoie un message texte complet au serveur (avec gestion du découpage/compression BluetoothTransferManager).
    /// </summary>
    public async Task<bool> SendMessageAsync(string message)
    {
        if (_characteristic == null)
        {
            LogMessage?.Invoke(this, "Impossible d'envoyer le message : non connecté.");
            return false;
        }

        await _sendSemaphore.WaitAsync();
        try
        {
            var chunks = _transferManager.PrepareMessagesForSending(message).ToList();

            foreach (var chunk in chunks)
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(chunk));
                var buffer = writer.DetachBuffer();

                var writeOption = _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                    ? GattWriteOption.WriteWithoutResponse
                    : GattWriteOption.WriteWithResponse;

                var result = await _characteristic.WriteValueWithResultAsync(buffer, writeOption);
                if (result.Status != GattCommunicationStatus.Success)
                {
                    // Repli : tenter avec l'autre mode d'écriture
                    var fallbackOption = writeOption == GattWriteOption.WriteWithoutResponse
                        ? GattWriteOption.WriteWithResponse
                        : GattWriteOption.WriteWithoutResponse;
                    result = await _characteristic.WriteValueWithResultAsync(buffer, fallbackOption);
                }

                if (result.Status != GattCommunicationStatus.Success)
                {
                    LogMessage?.Invoke(this, $"Échec d'envoi du paquet ({result.Status}).");
                    return false;
                }

                if (chunks.Count > 1)
                {
                    await Task.Delay(30); // Délai de sécurité MTU
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur d'envoi Bluetooth : {ex.Message}");
            return false;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private void OnDeviceConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            LogMessage?.Invoke(this, $"Le périphérique [{FormatAddress(sender.BluetoothAddress)}] s'est déconnecté.");
            Disconnected?.Invoke(this, FormatAddress(sender.BluetoothAddress));
        }
        else if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
        {
            LogMessage?.Invoke(this, $"Le périphérique [{FormatAddress(sender.BluetoothAddress)}] est connecté.");
            Connected?.Invoke(this, FormatAddress(sender.BluetoothAddress));
        }
    }

    private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            byte[] bytes = new byte[args.CharacteristicValue.Length];
            reader.ReadBytes(bytes);

            string chunk = Encoding.UTF8.GetString(bytes);
            string? fullMessage = _transferManager.ProcessReceivedData(chunk);

            if (fullMessage != null)
            {
                MessageReceived?.Invoke(this, fullMessage);
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur lors de la réception des données : {ex.Message}");
        }
    }

    public static string FormatAddress(ulong address)
    {
        return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
            (address >> 40) & 0xFF,
            (address >> 32) & 0xFF,
            (address >> 24) & 0xFF,
            (address >> 16) & 0xFF,
            (address >> 8) & 0xFF,
            address & 0xFF);
    }

    public static bool TryParseAddress(string input, out ulong address)
    {
        address = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string clean = input.Replace(":", "").Replace("-", "").Trim();
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(2);
        }

        if (ulong.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out address))
        {
            return true;
        }

        return ulong.TryParse(input, out address);
    }

    public void Dispose()
    {
        StopScan();
        _ = DisconnectAsync();
        _sendSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
