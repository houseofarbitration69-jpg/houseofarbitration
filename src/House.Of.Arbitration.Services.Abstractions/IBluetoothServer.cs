#region Imports
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.Services.Abstractions;

public interface IBluetoothServer
{
    #region Events
    event EventHandler<(string ClientId, string Message)> MessageReceived;
    event EventHandler<string> DeviceConnected;
    event EventHandler<string> DeviceDisconnected;
    #endregion

    #region Properties
    Guid InstanceId { get; }
    ObservableCollection<string> ConnectedClients { get; }
    #endregion

    #region Methods
    Task<bool> StartAdvertising(string serviceUuid, string deviceName);

    Task StopAdvertising();

    Task SendMessage(string message);

    Task SendToAllAsync(string message);

    Task SendToClientAsync(string message, string clientId);
    #endregion
}
