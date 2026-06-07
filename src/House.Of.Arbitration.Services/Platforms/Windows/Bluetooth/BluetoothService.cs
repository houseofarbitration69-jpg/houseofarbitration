#region Imports
using House.Of.Arbitration.Services.Abstractions;
using Windows.Devices.Bluetooth;
#endregion

namespace House.Of.Arbitration.Services.Platforms.Windows.Bluetooth;

public class BluetoothService : IBluetoothService
{
    public bool IsBluetoothAvailable => true;

    #region Implement IBluetoothService
    public async Task<bool> RequestBluetoothPermissions()
    {
        var bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();

        var locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        return bluetoothStatus == PermissionStatus.Granted && locationStatus == PermissionStatus.Granted;
    }
    #endregion

    #region Private Methods
    private BluetoothAdapter? GetBluetoothAdapter()
    {
        return null;
    }
    #endregion
}
