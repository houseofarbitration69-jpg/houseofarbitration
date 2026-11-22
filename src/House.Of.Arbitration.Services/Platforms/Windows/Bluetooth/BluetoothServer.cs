#region Imports
using House.Of.Arbitration.Services.Abstractions;
using System.Collections.ObjectModel;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
#endregion

namespace House.Of.Arbitration.Services.Platforms.Windows.Bluetooth;

public class BluetoothServer : IBluetoothServer, IDisposable
{
    public static readonly Guid ArbitrationServiceUuid = new("0000180F-0000-1000-8000-00805F9B34FB");
    public static readonly Guid ArbitrationCharacteristicUuid = new("00002A19-0000-1000-8000-00805F9B34FB");

    private readonly IAlertService _alertService;
    private readonly BluetoothTransferManager _transferManager = new();
    // Sémaphore dédié aux broadcasts (SendToAllAsync) : séparé du sémaphore unicast
    // pour que TIMER_START ne soit jamais bloqué par un envoi de données matchs/compétition.
    private readonly SemaphoreSlim _broadcastSemaphore = new(1, 1);
    // Sémaphore dédié aux envois ciblés (SendToClientAsync).
    private readonly SemaphoreSlim _unicastSemaphore = new(1, 1);
    private readonly HashSet<string> _knownSubscribedClients = new();

    private GattServiceProvider? _serviceProvider;
    private GattLocalCharacteristic? _characteristic;
    private BluetoothLEAdvertisementPublisher? _publisher;

    public event EventHandler<(string ClientId, string Message)>? MessageReceived;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;

    public Guid InstanceId { get; } = Guid.NewGuid();
    public ObservableCollection<string> ConnectedClients { get; } = new();
    public string DeviceName { get; private set; } = "HouseOfArbitration-Server";

    public BluetoothServer(IAlertService alertService)
    {
        _alertService = alertService;
    }

    public async Task<bool> StartAdvertising(string serviceUuid, string deviceName)
    {
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            DeviceName = deviceName.Trim();
        }

        Guid serviceGuid = ArbitrationServiceUuid;
        if (!string.IsNullOrWhiteSpace(serviceUuid) && Guid.TryParse(serviceUuid, out var parsedGuid))
        {
            serviceGuid = parsedGuid;
        }

        try
        {
            await StopAdvertising();

            var serviceResult = await GattServiceProvider.CreateAsync(serviceGuid);
            if (serviceResult.Error != BluetoothError.Success)
            {
                await _alertService.ShowToast($"Échec création service GATT : {serviceResult.Error}");
                return false;
            }

            _serviceProvider = serviceResult.ServiceProvider;

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
                await _alertService.ShowToast($"Échec création caractéristique GATT : {charResult.Error}");
                return false;
            }

            _characteristic = charResult.Characteristic;
            _characteristic.WriteRequested += OnCharacteristicWriteRequested;
            _characteristic.ReadRequested += OnCharacteristicReadRequested;
            _characteristic.SubscribedClientsChanged += OnSubscribedClientsChanged;

            var advParameters = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true
            };
            _serviceProvider.StartAdvertising(advParameters);

            try
            {
                _publisher = new BluetoothLEAdvertisementPublisher();
                _publisher.Advertisement.LocalName = DeviceName;

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

                _publisher.Start();
            }
            catch { }

            await _alertService.ShowToast($"Serveur BLE démarré : {DeviceName}");
            return true;
        }
        catch (Exception ex)
        {
            await _alertService.ShowToast($"Erreur BLE : {ex.Message}");
            return false;
        }
    }

    public Task StopAdvertising()
    {
        if (_publisher != null)
        {
            try { _publisher.Stop(); } catch { }
            _publisher = null;
        }

        if (_serviceProvider != null)
        {
            try { _serviceProvider.StopAdvertising(); } catch { }
            _serviceProvider = null;
        }

        _characteristic = null;

        lock (_knownSubscribedClients)
        {
            _knownSubscribedClients.Clear();
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectedClients.Clear();
        });

        _transferManager.Clear();
        return Task.CompletedTask;
    }

    public Task SendMessage(string message) => SendToAllAsync(message);

    public async Task SendToAllAsync(string message)
    {
        if (_characteristic == null) return;

        // Sémaphore broadcast indépendant du sémaphore unicast :
        // TIMER_START ne sera jamais bloqué par un envoi de COMPETITION_DATA/MATCH_INFO.
        // Timeout 1s : si un autre broadcast est en cours depuis plus d'1s, on abandonne.
        if (!await _broadcastSemaphore.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            return;
        }
        try
        {
            var chunks = _transferManager.PrepareMessagesForSending(message).ToList();
            foreach (var chunk in chunks)
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(chunk));
                var buffer = writer.DetachBuffer();

                bool notified = false;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                    await _characteristic.NotifyValueAsync(buffer).AsTask(cts.Token);
                    notified = true;
                }
                catch { }

                if (!notified)
                {
                    var subscribers = _characteristic.SubscribedClients.ToList();
                    foreach (var client in subscribers)
                    {
                        for (int attempt = 0; attempt < 2; attempt++)
                        {
                            try
                            {
                                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                                await _characteristic.NotifyValueAsync(buffer, client).AsTask(cts.Token);
                                break;
                            }
                            catch
                            {
                                if (attempt == 0)
                                {
                                    await Task.Delay(30);
                                }
                            }
                        }
                    }
                }

                if (chunks.Count > 1)
                {
                    await Task.Delay(60);
                }
            }
        }
        finally
        {
            _broadcastSemaphore.Release();
        }
    }

    public async Task SendToClientAsync(string message, string clientId)
    {
        if (_characteristic == null) return;
        if (string.IsNullOrWhiteSpace(clientId)) return;

        string targetAddress = FormatClientId(clientId.Contains('#') ? clientId.Split('#')[0] : clientId);
        if (string.IsNullOrWhiteSpace(targetAddress) || targetAddress.Equals("Client", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!await _unicastSemaphore.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            return;
        }
        try
        {
            // Trouver le client abonné correspondant à l'adresse ciblée
            var targetClient = _characteristic.SubscribedClients
                .LastOrDefault(c =>
                {
                    var devId = c.Session?.DeviceId?.Id;
                    if (string.IsNullOrWhiteSpace(devId)) return false;

                    string clientAddr = FormatClientId(devId);
                    return string.Equals(clientAddr, targetAddress, StringComparison.OrdinalIgnoreCase)
                        || devId.EndsWith(targetAddress, StringComparison.OrdinalIgnoreCase);
                });

            if (targetClient == null)
            {
                // Envoi ciblé : si le client n'est pas trouvé parmi les abonnés,
                // NE JAMAIS broadcaster aux autres juges !
                return;
            }

            var chunks = _transferManager.PrepareMessagesForSending(message).ToList();
            foreach (var chunk in chunks)
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(chunk));
                var buffer = writer.DetachBuffer();

                bool notified = false;
                for (int attempt = 0; attempt < 2 && !notified; attempt++)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                        await _characteristic.NotifyValueAsync(buffer, targetClient).AsTask(cts.Token);
                        notified = true;
                    }
                    catch
                    {
                        if (attempt == 0)
                        {
                            await Task.Delay(50);
                        }
                    }
                }

                if (chunks.Count > 1)
                {
                    await Task.Delay(60);
                }
            }
        }
        finally
        {
            _unicastSemaphore.Release();
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
        catch { }
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
        catch { }
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!ConnectedClients.Contains(client))
                {
                    ConnectedClients.Add(client);
                }
            });
            DeviceConnected?.Invoke(this, client);
        }

        foreach (var client in newlyDisconnected)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ConnectedClients.Remove(client);
            });
            DeviceDisconnected?.Invoke(this, client);
        }
    }

    private static string FormatClientId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return "Client";

        string cleaned = rawId.Trim();

        // Supprimer le préfixe avant '#' si présent (ex: "Bluetooth#Bluetooth<adapter>-<remote>")
        if (cleaned.Contains('#'))
        {
            var parts = cleaned.Split('#');
            cleaned = parts.Length >= 2 ? parts[^1] : cleaned;
        }

        // Si le format contient un séparateur '-' entre l'adaptateur local et l'adresse distante
        if (cleaned.Contains('-'))
        {
            var parts = cleaned.Split('-');
            if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[^1]))
            {
                cleaned = parts[^1];
            }
        }

        return cleaned;
    }

    public void Dispose()
    {
        _ = StopAdvertising();
        _broadcastSemaphore.Dispose();
        _unicastSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
