#region Imports
using House.Of.Arbitration.Services.Abstractions;
using System.Collections.ObjectModel;
using System.Text;
#endregion

namespace House.Of.Arbitration.Services.Platforms.Windows.Bluetooth;

public class BluetoothServer : IBluetoothServer
{
    #region Services
    private readonly IAlertService _alertService;
    #endregion

    #region Events
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
    #endregion

    #region Properties
    public ObservableCollection<string> ConnectedClients { get; } = new();
    #endregion

    #region Constructors
    public BluetoothServer(IAlertService alertService)
    {
        _alertService = alertService;
    }
    #endregion

    #region Implement IBluetoothServer
    public async Task<bool> StartAdvertising(string serviceUuid, string deviceName)
    {
        await _alertService.ShowToast("Bluetooth LE Server started advertising");
        return true;
    }

    public async Task StopAdvertising()
    {
        await _alertService.ShowToast("Bluetooth LE Server stopped advertising");
    }

    public async Task SendMessage(string message)
    {
        await SendToAllAsync(message);
    }

    public async Task SendToAllAsync(string message)
    {
        await _alertService.ShowToast($"Attempting to send to all : '{message}'");
    }

    public async Task SendToClientAsync(string message, string clientId)
    {
        await _alertService.ShowToast($"Attempting to send to client '{clientId}':'{message}'");
    }
    #endregion
}
