using BluetoothApp.Services;
using Android.Content;
using Android.Bluetooth;
using Android.App;
using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;

namespace BluetoothApp.Platforms.Android.Bluetooth;

public class AndroidBluetoothService : IBluetoothService
{
    public bool IsBluetoothAvailable => GetBluetoothAdapter() != null;

    public async Task<bool> RequestBluetoothPermissions()
    {
        var bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
        var locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        return bluetoothStatus == PermissionStatus.Granted && locationStatus == PermissionStatus.Granted;
    }

    public BluetoothAdapter? GetBluetoothAdapter()
    {
        var bluetoothManager = MauiApplication.Current.GetSystemService("bluetooth") as BluetoothManager;
        return bluetoothManager?.Adapter;
    }
}
