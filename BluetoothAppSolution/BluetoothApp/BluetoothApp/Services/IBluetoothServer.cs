using System.Collections.ObjectModel;

namespace BluetoothApp.Services;

public interface IBluetoothServer
{
    event EventHandler<string> MessageReceived;
    event EventHandler<string> DeviceConnected;
    event EventHandler<string> DeviceDisconnected;

    ObservableCollection<string> ConnectedClients { get; }

    Task<bool> StartAdvertising(string serviceUuid);
    Task StopAdvertising();
    Task SendMessage(string message);
    Task SendToAllAsync(string message);
    Task SendToClientAsync(string message, string clientId);
}
