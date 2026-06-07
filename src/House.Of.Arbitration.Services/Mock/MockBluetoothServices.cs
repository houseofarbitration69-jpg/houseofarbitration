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
    public Guid InstanceId { get; } = Guid.NewGuid();
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<(string DeviceId, string Name, int Rssi)>? DeviceDiscovered;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;

    public async Task StartScan()
    {
        await Task.Delay(1000);
        DeviceDiscovered?.Invoke(this, ("SERVEUR_KUNGFU_MOCK", "Serveur Mock A", -55));
        await Task.Delay(500);
        DeviceDiscovered?.Invoke(this, ("ARENA_SERVER_B", "Serveur Mock B", -75));
    }

    public Task StopScan() => Task.CompletedTask;

    public async Task ConnectToDevice(string deviceId)
    {
        await Task.Delay(500);
        DeviceConnected?.Invoke(this, deviceId);

        // Simulate receiving match info after connection in the new JSON format
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            var matchData = new
            {
                categoryName = "SANDA SENIORS -70KG",
                redName = "JEAN DUPONT",
                blueName = "MARC DURAND",
                matchNumber = 4
            };
            var json = System.Text.Json.JsonSerializer.Serialize(matchData);
            MessageReceived?.Invoke(this, $"MATCH_INFO:{json}");
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
    public event EventHandler<(string ClientId, string Message)>? MessageReceived;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;

    public Guid InstanceId { get; } = Guid.NewGuid();

    public ObservableCollection<string> ConnectedClients { get; } = new();

    public async Task<bool> StartAdvertising(string serviceUuid, string deviceName)
    {
        // Simulate a client connecting after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            string mockClientId = "JUDGE_MOCK_01";
            ConnectedClients.Add(mockClientId);
            DeviceConnected?.Invoke(this, mockClientId);
        });
        return true;
    }

    public Task StopAdvertising() => Task.CompletedTask;

    public Task SendMessage(string message) => Task.CompletedTask;

    public Task SendToAllAsync(string message) => Task.CompletedTask;

    public Task SendToClientAsync(string message, string clientId)
    {
        // For mock purposes, we could log or simulate receiving it back if needed
        return Task.CompletedTask;
    }
}
