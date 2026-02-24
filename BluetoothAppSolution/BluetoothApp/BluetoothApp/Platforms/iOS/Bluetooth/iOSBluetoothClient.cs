using BluetoothApp.Services;
using CoreBluetooth;
using CoreFoundation;
using Foundation;
using System.Text;

namespace BluetoothApp.Platforms.iOS.Bluetooth;

public class iOSBluetoothClient : CBCentralManagerDelegate, IBluetoothClient
{
    private CBCentralManager _centralManager;
    private Dictionary<string, CBPeripheral> _discoveredPeripherals = new Dictionary<string, CBPeripheral>();
    private CBPeripheral _connectedPeripheral;
    private CustomCBPeripheralDelegate _peripheralDelegate; // New instance of custom delegate
    private CBCharacteristic _readWriteCharacteristic;

    private readonly CBUUID ServiceUuid = CBUUID.FromString("0000180F-0000-1000-8000-00805F9B34FB"); // Example Battery Service UUID
    private readonly CBUUID CharacteristicUuid = CBUUID.FromString("00002A19-0000-1000-8000-00805F9B34FB"); // Example Battery Level Characteristic UUID


    public event EventHandler<string> MessageReceived;
    public event EventHandler<string> DeviceDiscovered;
    public event EventHandler<string> DeviceConnected;
    public event EventHandler<string> DeviceDisconnected;

    public iOSBluetoothClient()
    {
        _centralManager = new CBCentralManager(this, DispatchQueue.MainQueue);
    }

    public Task StartScan()
    {
        _discoveredPeripherals.Clear();
        // Scan for devices advertising our specific service UUID
        _centralManager.ScanForPeripherals(new CBUUID[] { ServiceUuid });
        Console.WriteLine("iOS Bluetooth LE Client started scanning.");
        return Task.CompletedTask;
    }

    public Task StopScan()
    {
        _centralManager.StopScan();
        Console.WriteLine("iOS Bluetooth LE Client stopped scanning.");
        return Task.CompletedTask;
    }

    public Task ConnectToDevice(string deviceId)
    {
        if (_discoveredPeripherals.TryGetValue(deviceId, out var peripheral))
        {
            _centralManager.ConnectPeripheral(peripheral);
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
        if (_connectedPeripheral != null && _connectedPeripheral.Identifier.AsString() == deviceId)
        {
            _centralManager.CancelPeripheralConnection(_connectedPeripheral);
            Console.WriteLine($"Disconnected from {deviceId}");
        }
        return Task.CompletedTask;
    }

    public async Task SendMessage(string deviceId, string message)
    {
        if (_connectedPeripheral != null && _readWriteCharacteristic != null)
        {
            var data = NSData.FromString(message, NSStringEncoding.UTF8);
            _connectedPeripheral.WriteValue(data, _readWriteCharacteristic, CBCharacteristicWriteType.WithResponse);
            Console.WriteLine($"iOS Client sent: {message}");
        }
        else
        {
            Console.WriteLine("Not connected to a peripheral or characteristic not found.");
        }
    }

    public override void UpdatedState(CBCentralManager central)
    {
        Console.WriteLine($"Central Manager State: {central.State}");
        if ((int)central.State == (int)CBCentralManagerState.PoweredOn)
        {
            Console.WriteLine("Central Manager powered on. Ready to scan.");
        }
        else
        {
            Console.WriteLine("Bluetooth is not available or powered on.");
        }
    }

    public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI)
    {
        if (!_discoveredPeripherals.ContainsKey(peripheral.Identifier.AsString()))
        {
            _discoveredPeripherals.Add(peripheral.Identifier.AsString(), peripheral);
            DeviceDiscovered?.Invoke(this, peripheral.Identifier.AsString());
            Console.WriteLine($"Discovered peripheral: {peripheral.Name ?? "Unknown"} ({peripheral.Identifier.AsString()})");
        }
    }

        public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
        {
            Console.WriteLine($"Connected to peripheral: {peripheral.Name ?? "Unknown"} ({peripheral.Identifier.AsString()})");
            _connectedPeripheral = peripheral;
            _peripheralDelegate = new CustomCBPeripheralDelegate(this); // Assign custom delegate
            _connectedPeripheral.Delegate = _peripheralDelegate; // Set the peripheral's delegate
            _connectedPeripheral.DiscoverServices();
            DeviceConnected?.Invoke(this, peripheral.Identifier.AsString());
        }
    
        // Nested class to handle CBPeripheralDelegate methods
        private class CustomCBPeripheralDelegate : CBPeripheralDelegate
        {
            private readonly iOSBluetoothClient _parent;

            public CustomCBPeripheralDelegate(iOSBluetoothClient parent)
            {
                _parent = parent;
            }

        public override void DiscoveredService(CBPeripheral peripheral, NSError? error)
            {
                if (error != null)
                {
                    Console.WriteLine($"Error discovering services: {error.LocalizedDescription}");
                    return;
                }

                foreach (var service in peripheral.Services)
                {
                    if (service.UUID.Equals(_parent.ServiceUuid))
                    {
                        peripheral.DiscoverCharacteristics(new CBUUID[] { _parent.CharacteristicUuid }, service);
                        return;
                    }
                }
            }

        public override void DiscoveredCharacteristics(CBPeripheral peripheral, CBService service, NSError? error)
        {
                if (error != null)
                {
                    Console.WriteLine($"Error discovering characteristics: {error.LocalizedDescription}");
                    return;
                }

                foreach (var characteristic in service.Characteristics)
                {
                    if (characteristic.UUID.Equals(_parent.CharacteristicUuid))
                    {
                        _parent._readWriteCharacteristic = characteristic;
                        peripheral.SetNotifyValue(true, characteristic); // Subscribe to notifications
                        return;
                    }
                }
            }

        public override void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        {
                if (error != null)
                {
                    Console.WriteLine($"Error updating value for characteristic: {error.LocalizedDescription}");
                    return;
                }

                if (characteristic.UUID.Equals(_parent.CharacteristicUuid))
                {
                    var message = characteristic.Value.ToString(NSStringEncoding.UTF8);
                    _parent.MessageReceived?.Invoke(_parent, message);
                    Console.WriteLine($"iOS Client received: {message}");
                }
            }

        public override void WroteCharacteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
        {
                if (error != null)
                {
                    Console.WriteLine($"Error writing value for characteristic: {error.LocalizedDescription}");
                }
                else
                {
                    Console.WriteLine("iOS Client did write value for characteristic.");
                }
            }
        }
    }