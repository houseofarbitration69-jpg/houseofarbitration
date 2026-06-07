#region Imports
using Android.Bluetooth;
using House.Of.Arbitration.Services.Abstractions;
#endregion

namespace House.Of.Arbitration.Services.Platforms.Android.Bluetooth;

public class BluetoothService : IBluetoothService
{
    public bool IsBluetoothAvailable => GetBluetoothAdapter() != null;

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
        var bluetoothManager = MauiApplication.Current.GetSystemService("bluetooth") as BluetoothManager;
        return bluetoothManager?.Adapter;
    }
    #endregion
}
