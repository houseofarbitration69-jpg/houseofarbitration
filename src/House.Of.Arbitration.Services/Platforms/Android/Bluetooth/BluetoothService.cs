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
        var advertiseStatus = await Permissions.RequestAsync<BluetoothAdvertisePermission>();
        var locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            return bluetoothStatus == PermissionStatus.Granted && advertiseStatus == PermissionStatus.Granted;
        }

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

public class BluetoothAdvertisePermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(31)
            ? new[] { (global::Android.Manifest.Permission.BluetoothAdvertise, true) }
            : Array.Empty<(string, bool)>();
}
