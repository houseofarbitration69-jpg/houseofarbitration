using House.Of.Arbitration.Services.Abstractions;
using System.Collections.ObjectModel;

namespace House.Of.Arbitration.Services.Mock;

public class MockBluetoothService : IBluetoothService
{
    public bool IsBluetoothAvailable => true;

    public Task<bool> RequestBluetoothPermissions() => Task.FromResult(true);
}

public class MockBluetoothClient : IBluetoothClient
{
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? DeviceDiscovered;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;

    public async Task StartScan()
    {
        await Task.Delay(1000);
        DeviceDiscovered?.Invoke(this, "SERVEUR_KUNGFU_MOCK");
        await Task.Delay(500);
        DeviceDiscovered?.Invoke(this, "ARENA_SERVER_B");
    }

    public Task StopScan() => Task.CompletedTask;

    public async Task ConnectToDevice(string deviceId)
    {
        await Task.Delay(500);
        DeviceConnected?.Invoke(this, deviceId);

        // Simulate receiving a match info after connection
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            MessageReceived?.Invoke(this, "CATÉGORIE: SANDA SENIORS -70KG\nMATCH #4: JEAN DUPONT VS MARC DURAND");
        });
    }

    public Task DisconnectFromDevice(string deviceId)
    {
        DeviceDisconnected?.Invoke(this, deviceId);
        return Task.CompletedTask;
    }

    public Task SendMessage(string deviceId, string message) => Task.CompletedTask;
}

public class MockBluetoothServer : IBluetoothServer
{
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;

    public ObservableCollection<string> ConnectedClients { get; } = new();

    public Task<bool> StartAdvertising(string serviceUuid, string deviceName) => Task.FromResult(true);

    public Task StopAdvertising() => Task.CompletedTask;

    public Task SendMessage(string message) => Task.CompletedTask;

    public Task SendToAllAsync(string message) => Task.CompletedTask;

    public Task SendToClientAsync(string message, string clientId) => Task.CompletedTask;
}
