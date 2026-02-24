using CoreBluetooth;
using CoreFoundation;
using Foundation;
using System.Text;
using UIKit; // Added this using directive

namespace TabletTalk.Services
{
    // Indique au compilateur de n'inclure ce fichier que pour la compilation iOS.
    public class GattServer : CBPeripheralManagerDelegate, IGattServer
    {
        public event EventHandler<byte[]> MessageReceived;
        public event EventHandler<string> StatusChanged;

        private CBPeripheralManager _peripheralManager;
        private CBMutableCharacteristic _chatCharacteristic;
        private CBMutableService _chatService;
        
        private Guid _serviceUuid;
        private Guid _characteristicUuid;

        public GattServer()
        {
        }

        public Task Start(Guid serviceUuid, Guid characteristicUuid)
        {
            _serviceUuid = serviceUuid;
            _characteristicUuid = characteristicUuid;
            // L'initialisation du manager déclenche DidUpdateState
            _peripheralManager = new CBPeripheralManager(this, new DispatchQueue("com.tablettalk.gatt"));
            return Task.CompletedTask;
        }

        public override void DidUpdateState(CBPeripheralManager peripheral)
        {
            StatusChanged?.Invoke(this, $"État du PeripheralManager: {peripheral.State}");
            if (peripheral.State == CBManagerState.PoweredOn)
            {
                var characteristicUuid = CBUUID.FromString(_characteristicUuid.ToString());
                _chatCharacteristic = new CBMutableCharacteristic(
                    characteristicUuid,
                    CBCharacteristicProperties.Write | CBCharacteristicProperties.Notify,
                    null, // La valeur est nulle au début
                    CBAttributePermissions.Writeable);
                
                var serviceUuid = CBUUID.FromString(_serviceUuid.ToString());
                _chatService = new CBMutableService(serviceUuid, true);
                _chatService.Characteristics = new CBMutableCharacteristic[] { _chatCharacteristic };

                _peripheralManager.AddService(_chatService);
            }
        }
        
        public override void ServiceAdded(CBPeripheralManager peripheral, CBService service, NSError error)
        {
            if (error != null)
            {
                StatusChanged?.Invoke(this, $"Erreur lors de l'ajout du service: {error.Description}");
                return;
            }

            StatusChanged?.Invoke(this, "Service ajouté, démarrage de la publicité...");

            var serviceUuidCbuuid = CBUUID.FromString(_serviceUuid.ToString());
            var serviceUuidsArray = NSArray.FromObjects(serviceUuidCbuuid);

            var advertisementData = new NSMutableDictionary
            {
                { CBAdvertisement.DataServiceUUIDsKey, serviceUuidsArray },
                { CBAdvertisement.DataLocalNameKey, new NSString(UIDevice.CurrentDevice.Name) }
            };
            _peripheralManager.StartAdvertising(advertisementData);
        }

        public Task Stop()
        {
            if (_peripheralManager != null)
            {
                _peripheralManager.StopAdvertising();
                _peripheralManager.RemoveAllServices();
            }
            StatusChanged?.Invoke(this, "Serveur GATT arrêté.");
            return Task.CompletedTask;
        }

        public Task SendMessageToSubscribers(byte[] message)
        {
            if (_chatCharacteristic != null)
            {
                _peripheralManager.UpdateValue(
                    NSData.FromArray(message),
                    _chatCharacteristic,
                    null); // Envoie à tous les abonnés
            }
            return Task.CompletedTask;
        }

        public override void ReceivedWriteRequests(CBPeripheralManager peripheral, CBATTRequest[] requests)
        {
            foreach (var request in requests)
            {
                if (request.Characteristic.UUID.Equals(_chatCharacteristic.UUID))
                {
                    MessageReceived?.Invoke(this, request.Value.ToArray());
                    peripheral.RespondToRequest(request, CBATTError.Success);
                }
            }
        }

        public override void DidSubscribeToCharacteristic(CBPeripheralManager peripheral, CBCentral central, CBCharacteristic characteristic)
        {
            StatusChanged?.Invoke(this, $"Central {central.Identifier} abonné à la caractéristique.");
        }

        public override void DidUnsubscribeFromCharacteristic(CBPeripheralManager peripheral, CBCentral central, CBCharacteristic characteristic)
        {
             StatusChanged?.Invoke(this, $"Central {central.Identifier} désabonné.");
        }
    }
}
