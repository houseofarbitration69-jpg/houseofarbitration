using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace House.Of.Arbitration.Monitor.Cli;

public class DiscoveredDevice
{
    public ulong Address { get; set; }
    public BluetoothAddressType AddressType { get; set; } = BluetoothAddressType.Public;
    public string AddressFormatted { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public short Rssi { get; set; }
    public bool HasArbitrationService { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public string DisplayName => !string.IsNullOrWhiteSpace(Name) 
        ? Name 
        : (HasArbitrationService ? $"Serveur ({AddressFormatted})" : "Inconnu");

    public override string ToString() =>
        $"[{AddressFormatted}] {DisplayName} (RSSI: {Rssi} dBm, Type: {AddressType})" + (HasArbitrationService ? " [SERVEUR ARBITRATION]" : "");
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
    private GattDeviceService? _service;
    private GattCharacteristic? _characteristic;
    private GattSession? _gattSession;

    public event EventHandler<DiscoveredDevice>? DeviceDiscovered;
    public event EventHandler<string>? Connected;
    public event EventHandler<string>? Disconnected;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? LogMessage;

    /// <summary>
    /// DÃ©clenchÃ© quand Windows rapporte une reconnexion radio BLE (sans ConnectAsync de notre cÃ´tÃ©).
    /// Utile pour rÃ©initialiser les compteurs d'Ã©chec dans la session de juge.
    /// </summary>
    public event EventHandler<string>? RadioReconnected;

    public bool IsConnected => _characteristic != null && !string.IsNullOrEmpty(ConnectedDeviceAddress);

    private string? _discoveredServerName;
    public string? ConnectedDeviceName => !string.IsNullOrWhiteSpace(_discoveredServerName) ? _discoveredServerName : _device?.Name;
    public string? ConnectedDeviceAddress { get; private set; }
    public string? LastConnectedDeviceAddress { get; private set; }
    public ulong? LastConnectedBluetoothAddress { get; private set; }
    public BluetoothAddressType LastAddressType { get; private set; } = BluetoothAddressType.Public;

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
    /// Scanne les pÃ©riphÃ©riques Bluetooth pendant une durÃ©e spÃ©cifiÃ©e.
    /// </summary>
    public async Task<IReadOnlyList<DiscoveredDevice>> ScanForDevicesAsync(TimeSpan duration)
    {
        StartScan();
        await Task.Delay(duration);
        StopScan();
        return DiscoveredDevices;
    }

    /// <summary>
    /// DÃ©marre le scan des pÃ©riphÃ©riques Bluetooth Low Energy.
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
        _watcher.Stopped += (s, e) => LogMessage?.Invoke(this, $"Scan Bluetooth arrÃªtÃ© (Raison: {e.Error}).");

        _watcher.Start();
        LogMessage?.Invoke(this, "Scan Bluetooth LE dÃ©marrÃ©...");
    }

    /// <summary>
    /// ArrÃªte le scan des pÃ©riphÃ©riques Bluetooth.
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
            LogMessage?.Invoke(this, "Scan Bluetooth LE arrÃªtÃ©.");
        }
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        ulong address = args.BluetoothAddress;
        BluetoothAddressType addressType = args.BluetoothAddressType;
        string addressStr = FormatAddress(address);

        string name = string.Empty;

        // 1. PrioritÃ© 1 : Nom propriÃ©taire transportÃ© dans ManufacturerData (CompanyId = BLE_MANUFACTURER_ID)
        var mfg = args.Advertisement.ManufacturerData
            .FirstOrDefault(m => m.CompanyId == Models.Constants.BLE_MANUFACTURER_ID);
        if (mfg != null && mfg.Data != null && mfg.Data.Length > 0)
        {
            try
            {
                var reader = DataReader.FromBuffer(mfg.Data);
                byte[] bytes = new byte[mfg.Data.Length];
                reader.ReadBytes(bytes);
                name = Encoding.UTF8.GetString(bytes).Trim();
            }
            catch { }
        }

        // 2. PrioritÃ© 2 : LocalName standard dans le paquet d'advertising BLE
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(args.Advertisement.LocalName))
        {
            name = args.Advertisement.LocalName.Trim();
        }

        bool hasService = args.Advertisement.ServiceUuids.Contains(ArbitrationServiceUuid) || mfg != null;

        DiscoveredDevice dev;

        lock (_discoveredDevices)
        {
            if (_discoveredDevices.TryGetValue(address, out var existing))
            {
                dev = existing;
                // Met Ã  jour le nom si on a un nom valide
                if (!string.IsNullOrWhiteSpace(name) && name != "Inconnu")
                {
                    dev.Name = name;
                }
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
                    Name = (!string.IsNullOrWhiteSpace(name) && name != "Inconnu") ? name : string.Empty,
                    Rssi = args.RawSignalStrengthInDBm,
                    HasArbitrationService = hasService,
                    LastSeen = DateTime.UtcNow
                };
                _discoveredDevices[address] = dev;
            }
        }

        // RÃ©solution du nom via le cache Windows Bluetooth uniquement si toujours inconnu
        if (string.IsNullOrWhiteSpace(dev.Name))
        {
            _ = TryResolveDeviceNameAsync(address);
        }

        DeviceDiscovered?.Invoke(this, dev);
    }

    /// <summary>
    /// Tente de rÃ©soudre le nom d'un pÃ©riphÃ©rique BLE via le cache Windows Bluetooth.
    /// UtilisÃ© comme fallback quand le LocalName n'est pas inclus dans le paquet d'advertising.
    /// </summary>
    private async Task TryResolveDeviceNameAsync(ulong bluetoothAddress)
    {
        try
        {
            // FromBluetoothAddressAsync interroge le cache systÃ¨me Bluetooth Windows
            var bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
            if (bleDevice != null && !string.IsNullOrWhiteSpace(bleDevice.Name))
            {
                lock (_discoveredDevices)
                {
                    if (_discoveredDevices.TryGetValue(bluetoothAddress, out var dev)
                        && string.IsNullOrWhiteSpace(dev.Name))
                    {
                        dev.Name = bleDevice.Name;
                    }
                }
            }
        }
        catch
        {
            // RÃ©solution silencieuse â€” pas critique
        }
    }

    /// <summary>
    /// Se connecte au pÃ©riphÃ©rique BLE spÃ©cifiÃ© par un objet DiscoveredDevice.
    /// </summary>
    public async Task<bool> ConnectAsync(DiscoveredDevice device)
    {
        return await ConnectAsync(device.Address, device.AddressType);
    }

    /// <summary>
    /// Se connecte au pÃ©riphÃ©rique BLE spÃ©cifiÃ© par son adresse Bluetooth (ulong).
    /// </summary>
    public async Task<bool> ConnectAsync(ulong bluetoothAddress, BluetoothAddressType addressType = BluetoothAddressType.Public)
    {
        // Ne pas dÃ©connecter si dÃ©jÃ  connectÃ© au mÃªme pÃ©riphÃ©rique (Ã©vite de couper une session active lors d'une tentative de reconnexion)
        string addrStr = FormatAddress(bluetoothAddress);
        if (IsConnected && ConnectedDeviceAddress == addrStr)
        {
            LogMessage?.Invoke(this, $"DÃ©jÃ  connectÃ© Ã  [{addrStr}], pas de reconnexion nÃ©cessaire.");
            return true;
        }

        await DisconnectAsync(notify: false, reason: $"Nettoyage avant connexion Ã  [{addrStr}]");

        lock (_discoveredDevices)
        {
            if (_discoveredDevices.TryGetValue(bluetoothAddress, out var dev))
            {
                addressType = dev.AddressType;
                if (!string.IsNullOrWhiteSpace(dev.Name) && dev.Name != "Inconnu")
                {
                    _discoveredServerName = dev.Name;
                }
            }
        }

        LogMessage?.Invoke(this, $"Connexion au pÃ©riphÃ©rique [{addrStr}] (AddressType: {addressType})...");

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
                LogMessage?.Invoke(this, $"Ã‰chec : Impossible d'obtenir le pÃ©riphÃ©rique Bluetooth [{addrStr}]. VÃ©rifiez que le Bluetooth est activÃ©.");
                return false;
            }

            ConnectedDeviceAddress = addrStr;
            LastConnectedDeviceAddress = addrStr;
            LastConnectedBluetoothAddress = bluetoothAddress;
            LastAddressType = addressType;
            _device.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;

            // === GattSession : maintien de la connexion physique BLE ===
            // MaintainConnection = true empÃªche Windows de fermer la liaison BLE aprÃ¨s inactivitÃ©.
            // IMPORTANT : on ne Dispose() PAS _gattSession dans DisconnectAsync pour Ã©viter
            // un nettoyage BLE asynchrone ~2s qui couperait la nouvelle connexion.
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
                // Non bloquant â€” la connexion peut continuer sans GattSession
            }

            // === DÃ©couverte du service GATT d'arbitrage ===
            // StratÃ©gie : Cached d'abord (Windows a souvent le GATT en cache aprÃ¨s reconnexion radio),
            // puis Uncached si le cache est vide ou pÃ©rimÃ©.
            LogMessage?.Invoke(this, $"Recherche du service GATT ({ArbitrationServiceUuid}) [Cached]...");
            var cachedServicesOp = _device.GetGattServicesForUuidAsync(ArbitrationServiceUuid, BluetoothCacheMode.Cached);
            var servicesResult = await WithTimeoutAsync(cachedServicesOp, TimeSpan.FromSeconds(3));

            if (servicesResult == null || servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                LogMessage?.Invoke(this, $"Cache GATT vide, tentative Uncached...");
                var uncachedServicesOp = _device.GetGattServicesForUuidAsync(ArbitrationServiceUuid, BluetoothCacheMode.Uncached);
                servicesResult = await WithTimeoutAsync(uncachedServicesOp, TimeSpan.FromSeconds(5));
            }

            if (servicesResult == null || servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                LogMessage?.Invoke(this, $"Service d'arbitrage non trouvÃ© ou timeout (Status={servicesResult?.Status}).");
                await DisconnectAsync(notify: false, reason: "Service GATT introuvable");
                return false;
            }

            _service = servicesResult.Services[0];

            // === DÃ©couverte de la caractÃ©ristique GATT ===
            LogMessage?.Invoke(this, $"Recherche de la caractÃ©ristique GATT ({ArbitrationCharacteristicUuid}) [Cached]...");
            var cachedCharOp = _service.GetCharacteristicsForUuidAsync(ArbitrationCharacteristicUuid, BluetoothCacheMode.Cached);
            var charResult = await WithTimeoutAsync(cachedCharOp, TimeSpan.FromSeconds(3));

            if (charResult == null || charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
            {
                LogMessage?.Invoke(this, $"Cache caractÃ©ristique vide, tentative Uncached...");
                var uncachedCharOp = _service.GetCharacteristicsForUuidAsync(ArbitrationCharacteristicUuid, BluetoothCacheMode.Uncached);
                charResult = await WithTimeoutAsync(uncachedCharOp, TimeSpan.FromSeconds(5));
            }

            if (charResult == null || charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
            {
                LogMessage?.Invoke(this, $"CaractÃ©ristique d'arbitrage introuvable ou timeout (Status={charResult?.Status}).");
                await DisconnectAsync(notify: false, reason: "CaractÃ©ristique GATT introuvable");
                return false;
            }

            _characteristic = charResult.Characteristics[0];

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

            LogMessage?.Invoke(this, $"ConnectÃ© avec succÃ¨s au serveur [{ConnectedDeviceName ?? addrStr}] !");
            Connected?.Invoke(this, addrStr);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur lors de la connexion Bluetooth : {ex.Message}");
            await DisconnectAsync(notify: false, reason: $"Exception ConnectAsync : {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Se connecte par chaÃ®ne d'adresse (ex: "AA:BB:CC:DD:EE:FF" ou "AABBCCDDEEFF") ou par nom de serveur.
    /// Si aucune adresse n'est fournie ou si la cible est un nom, recherche automatiquement le serveur BLE.
    /// </summary>
    public async Task<bool> ConnectAsync(string addressOrName)
    {
        if (string.IsNullOrWhiteSpace(addressOrName))
        {
            var server = await ScanForServerAsync(TimeSpan.FromSeconds(5));
            if (server != null)
            {
                return await ConnectAsync(server);
            }
            LogMessage?.Invoke(this, "Aucun serveur d'arbitrage trouvÃ© lors de la recherche automatique.");
            return false;
        }

        if (TryParseAddress(addressOrName, out ulong address))
        {
            return await ConnectAsync(address);
        }

        // Recherche dans le cache de scan
        DiscoveredDevice? match = null;
        lock (_discoveredDevices)
        {
            match = _discoveredDevices.Values.FirstOrDefault(d =>
                d.Name.Contains(addressOrName, StringComparison.OrdinalIgnoreCase) ||
                d.DisplayName.Contains(addressOrName, StringComparison.OrdinalIgnoreCase));
        }

        if (match != null)
        {
            return await ConnectAsync(match);
        }

        // Recherche active par scan BLE
        LogMessage?.Invoke(this, $"Recherche du serveur BLE '{addressOrName}'...");
        var scanned = await ScanForServerAsync(TimeSpan.FromSeconds(5));
        if (scanned != null)
        {
            return await ConnectAsync(scanned);
        }

        LogMessage?.Invoke(this, $"Format d'adresse ou nom Bluetooth introuvable : '{addressOrName}'.");
        return false;
    }

    /// <summary>
    /// Scanne briÃ¨vement les annonces BLE Ã  la recherche du serveur d'arbitrage.
    /// Retourne dÃ¨s que le serveur est dÃ©tectÃ© (sans attendre la fin du dÃ©lai).
    /// </summary>
    public async Task<DiscoveredDevice?> ScanForServerAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<DiscoveredDevice?>();

        void Handler(object? sender, DiscoveredDevice dev)
        {
            if ((LastConnectedBluetoothAddress.HasValue && dev.Address == LastConnectedBluetoothAddress.Value) ||
                dev.HasArbitrationService)
            {
                tcs.TrySetResult(dev);
            }
        }

        DeviceDiscovered += Handler;
        StartScan();

        try
        {
            var delayTask = Task.Delay(timeout);
            var completed = await Task.WhenAny(tcs.Task, delayTask);
            if (completed == tcs.Task)
            {
                return await tcs.Task;
            }
            return null;
        }
        finally
        {
            DeviceDiscovered -= Handler;
            StopScan();
        }
    }

    /// <summary>
    /// Tente de se reconnecter au dernier pÃ©riphÃ©rique serveur connu.
    /// Lance un scan express pour capter l'annonce BLE, rafraÃ®chir le cache Windows et se connecter.
    /// </summary>
    public async Task<bool> ReconnectAsync()
    {
        if (IsConnected) return true;

        // 1. Scan express pour vÃ©rifier que le serveur est actif et actualiser le cache Windows BLE
        var found = await ScanForServerAsync(TimeSpan.FromSeconds(2));
        if (found != null)
        {
            LogMessage?.Invoke(this, $"Serveur d'arbitrage dÃ©tectÃ© [{found.AddressFormatted}] ! Connexion...");
            return await ConnectAsync(found);
        }

        // 2. Si le scan n'a rien donnÃ© mais qu'on a une adresse directe, tenter avec timeout court
        if (LastConnectedBluetoothAddress.HasValue)
        {
            LogMessage?.Invoke(this, $"Tentative de connexion directe vers [{LastConnectedDeviceAddress}]...");
            try
            {
                bool ok = await ConnectAsync(LastConnectedBluetoothAddress.Value, LastAddressType);
                if (ok) return true;
            }
            catch { }
        }

        return false;
    }

    private static async Task<T?> WithTimeoutAsync<T>(Windows.Foundation.IAsyncOperation<T> operation, TimeSpan timeout)
    {
        var opTask = operation.AsTask();
        var delayTask = Task.Delay(timeout);
        var completed = await Task.WhenAny(opTask, delayTask);
        if (completed == opTask)
        {
            return await opTask;
        }

        try { operation.Cancel(); } catch { }
        return default;
    }

    /// <summary>
    /// DÃ©connecte le pÃ©riphÃ©rique actuel.
    /// </summary>
    public async Task DisconnectAsync(bool notify = true, string reason = "")
    {
        string? addr = ConnectedDeviceAddress;
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string reasonText = string.IsNullOrEmpty(reason) ? "(non spÃ©cifiÃ©e)" : reason;
        LogMessage?.Invoke(this, $"[{timestamp}] [DISCONNECT] AppelÃ© â€” Raison : {reasonText} | addr={addr ?? "null"} | notify={notify}");

        if (_characteristic != null)
        {
            try
            {
                _characteristic.ValueChanged -= OnCharacteristicValueChanged;
                var writeOp = _characteristic.WriteClientCharacteristicConfigurationDescriptorWithResultAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
                await WithTimeoutAsync(writeOp, TimeSpan.FromMilliseconds(500));
            }
            catch { }
            _characteristic = null;
        }

        // DÃ©sactiver MaintainConnection SANS Dispose (Ã©vite le nettoyage BLE asynchrone ~2s
        // qui couperait la nouvelle connexion si un ReconnectAsync est en cours).
        if (_gattSession != null)
        {
            try { _gattSession.MaintainConnection = false; } catch { }
            // NE PAS appeler _gattSession.Dispose() ici â€” le GC nettoie sans effet secondaire BLE.
            _gattSession = null;
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

        if (notify && !string.IsNullOrEmpty(addr))
        {
            Disconnected?.Invoke(this, addr);
        }
    }

    /// <summary>
    /// Envoie un message texte complet au serveur (avec gestion du dÃ©coupage/compression BluetoothTransferManager).
    /// </summary>
    public async Task<bool> SendMessageAsync(string message)
    {
        if (_characteristic == null)
        {
            LogMessage?.Invoke(this, "Impossible d'envoyer le message : non connectÃ©.");
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

                // PrÃ©fÃ©rer WriteWithResponse pour la fiabilitÃ© et le support des charges utiles longues (GATT Long Write)
                var writeOption = _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write)
                    ? GattWriteOption.WriteWithResponse
                    : (_characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                        ? GattWriteOption.WriteWithoutResponse
                        : GattWriteOption.WriteWithResponse);

                var result = await _characteristic.WriteValueWithResultAsync(buffer, writeOption);
                if (result.Status != GattCommunicationStatus.Success)
                {
                    // Repli : tenter avec l'autre mode d'Ã©criture
                    var fallbackOption = writeOption == GattWriteOption.WriteWithResponse
                        ? GattWriteOption.WriteWithoutResponse
                        : GattWriteOption.WriteWithResponse;
                    result = await _characteristic.WriteValueWithResultAsync(buffer, fallbackOption);
                }

                if (result.Status != GattCommunicationStatus.Success)
                {
                    LogMessage?.Invoke(this, $"Ã‰chec d'envoi du paquet ({result.Status}).");
                    return false;
                }

                if (chunks.Count > 1)
                {
                    await Task.Delay(30); // DÃ©lai de sÃ©curitÃ© MTU
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur d'envoi Bluetooth : {ex.Message}.");
            return false;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private void OnDeviceConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            LogMessage?.Invoke(this, $"[{timestamp}] [RADIO-BLE] Statut radio : DÃ‰CONNECTÃ‰ (inactif/veille). ConnectedDeviceAddress={ConnectedDeviceAddress}");
            // Note : Windows WinRT passe souvent en ConnectionStatus.Disconnected lors de brÃ¨ves pÃ©riodes d'inactivitÃ© ou d'une coupure BT.
            // La rupture rÃ©elle de la session applicative est gÃ©rÃ©e par l'Ã©chec du prochain ping (watchdog).
        }
        else if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
        {
            LogMessage?.Invoke(this, $"[{timestamp}] [RADIO-BLE] Statut radio : CONNECTÃ‰. ConnectedDeviceAddress={ConnectedDeviceAddress}");
            // La radio BLE s'est reconnectÃ©e (Windows a rÃ©tabli la liaison sans ConnectAsync de notre cÃ´tÃ©).
            // On notifie la session de juge pour qu'elle puisse rÃ©initialiser les compteurs d'Ã©chec.
            if (!string.IsNullOrEmpty(ConnectedDeviceAddress))
            {
                RadioReconnected?.Invoke(this, ConnectedDeviceAddress);
            }
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
            LogMessage?.Invoke(this, $"Erreur lors de la rÃ©ception des donnÃ©es : {ex.Message}");
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
        if (_gattSession != null)
        {
            try { _gattSession.MaintainConnection = false; _gattSession.Dispose(); } catch { }
            _gattSession = null;
        }
        _ = DisconnectAsync();
        _sendSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
