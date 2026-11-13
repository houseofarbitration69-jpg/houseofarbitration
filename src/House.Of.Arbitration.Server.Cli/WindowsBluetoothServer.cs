using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace House.Of.Arbitration.Server.Cli;

/// <summary>
/// Serveur Bluetooth Low Energy natif Windows (WinRT GATT Server + BLE Advertisement Publisher).
/// </summary>
public class WindowsBluetoothServer : IDisposable
{
    public static readonly Guid ArbitrationServiceUuid = new("0000180F-0000-1000-8000-00805F9B34FB");
    public static readonly Guid ArbitrationCharacteristicUuid = new("00002A19-0000-1000-8000-00805F9B34FB");

    private readonly BluetoothTransferManager _transferManager = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private readonly HashSet<string> _knownSubscribedClients = new();

    private GattServiceProvider? _serviceProvider;
    private GattLocalCharacteristic? _characteristic;
    private BluetoothLEAdvertisementPublisher? _publisher;

    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<(string ClientId, string Message)>? MessageReceived;
    public event EventHandler<string>? LogMessage;

    public bool IsAdvertising => _serviceProvider?.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started;
    public string DeviceName { get; private set; } = "HouseOfArbitration-Server";
    public IReadOnlyCollection<string> ConnectedClientIds
    {
        get
        {
            lock (_knownSubscribedClients)
            {
                return _knownSubscribedClients.ToList();
            }
        }
    }

    /// <summary>
    /// Démarre le serveur GATT et commence la diffusion BLE.
    /// </summary>
    public async Task<bool> StartAdvertisingAsync(string? deviceName = null)
    {
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            DeviceName = deviceName.Trim();
        }

        try
        {
            StopAdvertising();

            LogMessage?.Invoke(this, $"Création du service GATT ({ArbitrationServiceUuid})...");
            var serviceResult = await GattServiceProvider.CreateAsync(ArbitrationServiceUuid);
            if (serviceResult.Error != BluetoothError.Success)
            {
                LogMessage?.Invoke(this, $"Échec création service GATT : {serviceResult.Error}");
                return false;
            }

            _serviceProvider = serviceResult.ServiceProvider;

            // Configuration de la caractéristique
            var charParameters = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Read |
                                           GattCharacteristicProperties.Write |
                                           GattCharacteristicProperties.WriteWithoutResponse |
                                           GattCharacteristicProperties.Notify,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                ReadProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "Arbitration Exchange Characteristic"
            };

            var charResult = await _serviceProvider.Service.CreateCharacteristicAsync(ArbitrationCharacteristicUuid, charParameters);
            if (charResult.Error != BluetoothError.Success)
            {
                LogMessage?.Invoke(this, $"Échec création caractéristique GATT : {charResult.Error}");
                return false;
            }

            _characteristic = charResult.Characteristic;
            _characteristic.WriteRequested += OnCharacteristicWriteRequested;
            _characteristic.ReadRequested += OnCharacteristicReadRequested;
            _characteristic.SubscribedClientsChanged += OnSubscribedClientsChanged;

            // Lancement de la diffusion de service connectable
            var advParameters = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true
            };
            _serviceProvider.StartAdvertising(advParameters);

            // Diffusion complémentaire avec le nom local pour faciliter la détection lors du scan.
            // Le ServiceUuid est déjà diffusé par GattServiceProvider.StartAdvertising().
            // On inclut le LocalName et une section ManufacturerData propriétaire (BLE_MANUFACTURER_ID = 0x4B46)
            // contenant le nom personnalisé du serveur. Ainsi le nom est garanti d'être transmis et reçu
            // par le client sans risque d'être ignoré ou écrasé par le nom système Windows.
            try
            {
                _publisher = new BluetoothLEAdvertisementPublisher();
                _publisher.Advertisement.LocalName = DeviceName;

                // Ajout de ManufacturerData propriétaire contenant le nom configuré
                try
                {
                    var mfg = new BluetoothLEManufacturerData
                    {
                        CompanyId = Models.Constants.BLE_MANUFACTURER_ID
                    };
                    var writer = new DataWriter();
                    writer.WriteString(DeviceName);
                    mfg.Data = writer.DetachBuffer();
                    _publisher.Advertisement.ManufacturerData.Add(mfg);
                }
                catch { }

                _publisher.StatusChanged += (s, e) =>
                {
                    if (e.Status == BluetoothLEAdvertisementPublisherStatus.Aborted)
                    {
                        LogMessage?.Invoke(this, $"Avertissement diffuseur BLE interrompu : {e.Error}");
                    }
                };

                _publisher.Start();
                LogMessage?.Invoke(this, $"Diffuseur BLE actif avec le nom : '{DeviceName}'.");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Avertissement éditeur d'annonce BLE : {ex.Message}");
            }

            LogMessage?.Invoke(this, $"Serveur GATT démarré et en attente de connexions de juges !");
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur démarrage serveur BLE : {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Arrête la diffusion et le serveur GATT.
    /// </summary>
    public void StopAdvertising()
    {
        if (_publisher != null)
        {
            try
            {
                _publisher.Stop();
            }
            catch { }
            _publisher = null;
        }

        if (_serviceProvider != null)
        {
            try
            {
                _serviceProvider.StopAdvertising();
            }
            catch { }
            _serviceProvider = null;
        }

        _characteristic = null;

        lock (_knownSubscribedClients)
        {
            _knownSubscribedClients.Clear();
        }

        _transferManager.Clear();
        LogMessage?.Invoke(this, "Serveur BLE arrêté.");
    }

    /// <summary>
    /// Diffuse un message à tous les juges connectés et abonnés.
    /// </summary>
    public async Task<bool> SendToAllAsync(string message)
    {
        if (_characteristic == null) return false;

        await _sendSemaphore.WaitAsync();
        try
        {
            var chunks = _transferManager.PrepareMessagesForSending(message).ToList();
            foreach (var chunk in chunks)
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(chunk));
                var buffer = writer.DetachBuffer();

                await _characteristic.NotifyValueAsync(buffer);

                if (chunks.Count > 1)
                {
                    await Task.Delay(30);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur diffusion vers tous : {ex.Message}");
            return false;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    /// <summary>
    /// Envoie un message à un client juge spécifique.
    /// </summary>
    public async Task<bool> SendToClientAsync(string clientId, string message)
    {
        if (_characteristic == null) return false;

        await _sendSemaphore.WaitAsync();
        try
        {
            var client = _characteristic.SubscribedClients.FirstOrDefault(c =>
                c.Session?.DeviceId?.Id != null &&
                (c.Session.DeviceId.Id.Contains(clientId, StringComparison.OrdinalIgnoreCase) ||
                 FormatClientId(c.Session.DeviceId.Id).Contains(clientId, StringComparison.OrdinalIgnoreCase)));

            var chunks = _transferManager.PrepareMessagesForSending(message).ToList();
            foreach (var chunk in chunks)
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(chunk));
                var buffer = writer.DetachBuffer();

                if (client != null)
                {
                    await _characteristic.NotifyValueAsync(buffer, client);
                }
                else
                {
                    // Si le client précis n'a pas été distingué, on notifie globalement
                    await _characteristic.NotifyValueAsync(buffer);
                }

                if (chunks.Count > 1)
                {
                    await Task.Delay(30);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur envoi vers client '{clientId}' : {ex.Message}");
            return false;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private async void OnCharacteristicWriteRequested(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync();
            if (request == null) return;

            string raw = string.Empty;
            if (request.Value != null && request.Value.Length > 0)
            {
                var reader = DataReader.FromBuffer(request.Value);
                byte[] bytes = new byte[request.Value.Length];
                reader.ReadBytes(bytes);
                raw = Encoding.UTF8.GetString(bytes);
            }

            string clientKey = FormatClientId(args.Session?.DeviceId?.Id ?? "remote_device");

            if (request.Option == GattWriteOption.WriteWithResponse)
            {
                request.Respond();
            }

            if (!string.IsNullOrEmpty(raw))
            {
                string? fullMessage = _transferManager.ProcessReceivedData(raw);
                if (fullMessage != null)
                {
                    MessageReceived?.Invoke(this, (clientKey, fullMessage));
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur requête écriture GATT : {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnCharacteristicReadRequested(GattLocalCharacteristic sender, GattReadRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync();
            if (request != null)
            {
                var writer = new DataWriter();
                writer.WriteString(DateTime.Now.ToLongTimeString());
                request.RespondWithValue(writer.DetachBuffer());
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Erreur requête lecture GATT : {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnSubscribedClientsChanged(GattLocalCharacteristic sender, object args)
    {
        var currentSubscribers = sender.SubscribedClients
            .Select(c => FormatClientId(c.Session?.DeviceId?.Id ?? "unknown"))
            .ToHashSet();

        List<string> newlyConnected;
        List<string> newlyDisconnected;

        lock (_knownSubscribedClients)
        {
            newlyConnected = currentSubscribers.Except(_knownSubscribedClients).ToList();
            newlyDisconnected = _knownSubscribedClients.Except(currentSubscribers).ToList();

            foreach (var client in newlyConnected)
            {
                _knownSubscribedClients.Add(client);
            }
            foreach (var client in newlyDisconnected)
            {
                _knownSubscribedClients.Remove(client);
            }
        }

        foreach (var client in newlyConnected)
        {
            LogMessage?.Invoke(this, $"Client BLE connecté/abonné : {client}");
            ClientConnected?.Invoke(this, client);
        }

        foreach (var client in newlyDisconnected)
        {
            LogMessage?.Invoke(this, $"Client BLE déconnecté : {client}");
            ClientDisconnected?.Invoke(this, client);
        }
    }

    private static string FormatClientId(string rawId)
    {
        // Nettoie l'ID d'appareil Windows pour le rendre compact et lisible
        if (string.IsNullOrWhiteSpace(rawId)) return "Client";

        if (rawId.Contains('#'))
        {
            var parts = rawId.Split('#');
            if (parts.Length >= 2) return parts[1];
        }

        return rawId.Length > 17 ? rawId.Substring(rawId.Length - 17) : rawId;
    }

    public void Dispose()
    {
        StopAdvertising();
        _sendSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
