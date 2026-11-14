namespace House.Of.Arbitration.Services.Abstractions;

public interface IBluetoothClient
{
    Guid InstanceId { get; }
    event EventHandler<string> MessageReceived;
    event EventHandler<(string DeviceId, string Name, int Rssi)> DeviceDiscovered;
    event EventHandler<string> DeviceConnected;
    event EventHandler<string> DeviceDisconnected;
    event EventHandler<int> RssiUpdated;

    Task StartScan();

    Task StopScan();

    Task ConnectToDevice(string deviceId);

    Task DisconnectFromDevice(string deviceId);

    Task SendMessage(string deviceId, string message);

    Task ReadRssiAsync();
}
