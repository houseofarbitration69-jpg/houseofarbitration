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
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
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

        await _sendSemaphore.WaitAsync();
        try
        {
            var chunks = _transferManager.PrepareMessagesForSending(message).ToList();
            foreach (var chunk in chunks)
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(chunk));
                var buffer = writer.DetachBuffer();

                var subscribers = _characteristic.SubscribedClients.ToList();
                if (subscribers.Count == 0)
                {
                    try { await _characteristic.NotifyValueAsync(buffer); } catch { }
                }
                else
                {
                    foreach (var client in subscribers)
                    {
                        try { await _characteristic.NotifyValueAsync(buffer, client); } catch { }
                    }
                }

                if (chunks.Count > 1)
                {
                    await Task.Delay(30);
                }
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public async Task SendToClientAsync(string message, string clientId)
    {
        if (_characteristic == null) return;

        await _sendSemaphore.WaitAsync();
        try
        {
            var matchingClients = _characteristic.SubscribedClients.Where(c =>
                c.Session?.DeviceId?.Id != null &&
                (c.Session.DeviceId.Id.Contains(clientId, StringComparison.OrdinalIgnoreCase) ||
                 FormatClientId(c.Session.DeviceId.Id).Contains(clientId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var chunks = _transferManager.PrepareMessagesForSending(message).ToList();
            foreach (var chunk in chunks)
            {
                var writer = new DataWriter();
                writer.WriteBytes(Encoding.UTF8.GetBytes(chunk));
                var buffer = writer.DetachBuffer();

                if (matchingClients.Count > 0)
                {
                    bool sent = false;
                    for (int i = matchingClients.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            await _characteristic.NotifyValueAsync(buffer, matchingClients[i]);
                            sent = true;
                            break;
                        }
                        catch { }
                    }

                    if (!sent)
                    {
                        try { await _characteristic.NotifyValueAsync(buffer); } catch { }
                    }
                }
                else
                {
                    try { await _characteristic.NotifyValueAsync(buffer); } catch { }
                }

                if (chunks.Count > 1)
                {
                    await Task.Delay(30);
                }
            }
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

        if (rawId.Contains('#'))
        {
            var parts = rawId.Split('#');
            if (parts.Length >= 2) return parts[1];
        }

        return rawId.Length > 17 ? rawId.Substring(rawId.Length - 17) : rawId;
    }

    public void Dispose()
    {
        _ = StopAdvertising();
        _sendSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
