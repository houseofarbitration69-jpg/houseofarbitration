#region Imports
using House.Of.Arbitration.Services.Abstractions;
using System.Text;
#endregion

namespace House.Of.Arbitration.Services.Platforms.Windows.Bluetooth;

public class BluetoothClient : IBluetoothClient
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    #region Services
    private readonly IAlertService _alertService;
    #endregion

    #region Events
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<(string DeviceId, string Name, int Rssi)>? DeviceDiscovered;
    public event EventHandler<string>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
    #endregion

    #region Constructors
    public BluetoothClient(IAlertService alertService)
    {
        _alertService = alertService;
    }
    #endregion

    #region Public Methods
    public async Task StartScan()
    {
        await _alertService.ShowToast("Bluetooth LE Client started scanning.");
    }

    public async Task StopScan()
    {
        await _alertService.ShowToast("Bluetooth LE Client stopped scanning.");
    }

    public async Task ConnectToDevice(string deviceId)
    {
        await _alertService.ShowToast($"Device {deviceId} not found");
    }

    public async Task DisconnectFromDevice(string deviceId)
    {
        await _alertService.ShowToast($"Disconnected from {deviceId}");
    }

    public async Task SendMessage(string deviceId, string message)
    {
        await _alertService.ShowToast($"Client sent : {message}");
    }
    #endregion
}
