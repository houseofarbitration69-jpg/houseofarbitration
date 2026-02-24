using System.Collections.ObjectModel;
using BluetoothApp.Services;
using CoreBluetooth;
using CoreFoundation;
using Foundation;
using System.Text;

namespace BluetoothApp.Platforms.iOS.Bluetooth;

public class iOSBluetoothServer : CBPeripheralManagerDelegate, IBluetoothServer
{
    private CBPeripheralManager _peripheralManager;
    private CBMutableService _primaryService;
    private CBMutableCharacteristic _readWriteCharacteristic;
    private bool _isAdvertising = false;
    private List<CBCentral> _subscribedCentrals = new List<CBCentral>();

    // A unique UUID for our service
    private readonly CBUUID ServiceUuid = CBUUID.FromString("0000180F-0000-1000-8000-00805F9B34FB"); // Example Battery Service UUID
    private readonly CBUUID CharacteristicUuid = CBUUID.FromString("00002A19-0000-1000-8000-00805F9B34FB"); // Example Battery Level Characteristic UUID

    public event EventHandler<string> MessageReceived;
    public event EventHandler<string> DeviceConnected;
    public event EventHandler<string> DeviceDisconnected;

    public ObservableCollection<string> ConnectedClients { get; } = new();

    public iOSBluetoothServer()
    {
        _peripheralManager = new CBPeripheralManager(this, DispatchQueue.MainQueue);
    }

    public async Task<bool> StartAdvertising(string serviceUuid)
    {
        if (((int)_peripheralManager.State) == ((int)CBManagerState.PoweredOn))
        {
            _readWriteCharacteristic = new CBMutableCharacteristic(
                CharacteristicUuid,
                CBCharacteristicProperties.Read | CBCharacteristicProperties.Write | CBCharacteristicProperties.Notify,
                null, // No value initially
                CBAttributePermissions.Readable | CBAttributePermissions.Writeable
            );

            _primaryService = new CBMutableService(ServiceUuid, true); // Primary Service
            _primaryService.Characteristics = new CBCharacteristic[] { _readWriteCharacteristic };

            _peripheralManager.AddService(_primaryService);

            var advertisementData = new NSDictionary(
                CBAdvertisement.DataLocalNameKey, "BluetoothAppServer",
                CBAdvertisement.DataServiceUUIDsKey, new CBUUID[] { ServiceUuid }
            );

            _peripheralManager.StartAdvertising(advertisementData);
            _isAdvertising = true;
            Console.WriteLine("iOS Bluetooth LE Server started advertising.");
            return true;
        }
        else
        {
            Console.WriteLine($"CBPeripheralManager not powered on. State: {_peripheralManager.State}");
            return false;
        }
    }

    public async Task StopAdvertising()
    {
        _peripheralManager.StopAdvertising();
        _isAdvertising = false;
        _peripheralManager.RemoveAllServices();
        Console.WriteLine("iOS Bluetooth LE Server stopped advertising.");
    }

    public async Task SendMessage(string message)
    {
        await SendToAllAsync(message);
    }

    public async Task SendToAllAsync(string message)
    {
        if (_readWriteCharacteristic != null && _subscribedCentrals.Any())
        {
            var data = NSData.FromString(message, NSStringEncoding.UTF8);
            _peripheralManager.UpdateValue(data, _readWriteCharacteristic, _subscribedCentrals.ToArray());
            Console.WriteLine($"iOS Server sent: {message}");
        }
        else
        {
            Console.WriteLine("No characteristic or subscribed centrals to send message to.");
        }
    }

    public async Task SendToClientAsync(string message, string clientId)
    {
        var central = _subscribedCentrals.FirstOrDefault(c => c.Identifier.AsString() == clientId);
        if (central != null)
        {
            var data = NSData.FromString(message, NSStringEncoding.UTF8);
            _peripheralManager.UpdateValue(data, _readWriteCharacteristic, new CBCentral[] { central });
        }
    }

    [Export("peripheralManagerDidUpdateState:")]
public void DidUpdateState(CBPeripheralManager peripheral)
    {
        Console.WriteLine($"Peripheral Manager State: {peripheral.State}");
        if (((int)peripheral.State) == ((int)CBManagerState.PoweredOn) && _primaryService != null && !_isAdvertising)
        {
            // If the manager was initialized but not advertising, start advertising.
            // This can happen if the manager initializes before permissions are granted.
            Console.WriteLine("Peripheral Manager powered on, restarting advertising.");
            StartAdvertising(ServiceUuid.ToString());
        }
    }

    [Export("peripheralManager:didAddService:error:")]
public void DidAddService(CBPeripheralManager peripheral, CBService service, NSError error)
    {
        if (error != null)
        {
            Console.WriteLine($"Error adding service: {error.LocalizedDescription}");
        }
        else
        {
            Console.WriteLine($"Service added: {service.UUID}");
        }
    }

    [Export("peripheralManager:didReceiveReadRequest:")]
public void DidReceiveReadRequest(CBPeripheralManager peripheral, CBATTRequest request)
    {
        if (request.Characteristic.UUID.Equals(CharacteristicUuid))
        {
            // For simplicity, let's just return the current time
            var value = NSData.FromString(DateTime.Now.ToLongTimeString(), NSStringEncoding.UTF8);
            request.Value = value;
            _peripheralManager.RespondToRequest(request, CBATTError.Success);
            Console.WriteLine($"iOS Server received read request and responded with: {DateTime.Now.ToLongTimeString()}");
        }
        else
        {
            _peripheralManager.RespondToRequest(request, CBATTError.ReadNotPermitted);
        }
    }

    [Export("peripheralManager:didReceiveWriteRequests:")]
public void DidReceiveWriteRequests(CBPeripheralManager peripheral, CBATTRequest[] requests)
    {
        foreach (var request in requests)
        {
            if (request.Characteristic.UUID.Equals(CharacteristicUuid))
            {
                var message = request.Value.ToString(NSStringEncoding.UTF8);
                Console.WriteLine($"iOS Server received write request: {message}");
                MessageReceived?.Invoke(this, message);
                _peripheralManager.RespondToRequest(request, CBATTError.Success);
            }
            else
            {
                _peripheralManager.RespondToRequest(request, CBATTError.WriteNotPermitted);
            }
        }
    }

    [Export("peripheralManager:central:didSubscribeToCharacteristic:")]
    public void CentralDidSubscribeToCharacteristic(CBPeripheralManager peripheral, CBCentral central, CBCharacteristic characteristic)
    {
        _subscribedCentrals.Add(central);
        ConnectedClients.Add(central.Identifier.AsString());
        Console.WriteLine($"Central {central.Identifier.AsString()} subscribed to characteristic {characteristic.UUID}");
        DeviceConnected?.Invoke(this, central.Identifier.AsString());
    }


    public override void CharacteristicUnsubscribed(CBPeripheralManager peripheral, CBCentral central, CBCharacteristic characteristic)
    {
        _subscribedCentrals.Remove(central);
        ConnectedClients.Remove(central.Identifier.AsString());
        Console.WriteLine($"Central {central.Identifier.AsString()} unsubscribed from characteristic {characteristic.UUID}");
        DeviceDisconnected?.Invoke(this, central.Identifier.AsString());
    }
}