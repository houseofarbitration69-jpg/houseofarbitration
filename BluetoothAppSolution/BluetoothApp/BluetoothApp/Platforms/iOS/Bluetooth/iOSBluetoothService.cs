using BluetoothApp.Services;
using CoreBluetooth;
using UIKit;

namespace BluetoothApp.Platforms.iOS.Bluetooth;

public class iOSBluetoothService : IBluetoothService
{
    private CBCentralManager _centralManager;

    public iOSBluetoothService()
    {
        _centralManager = new CBCentralManager();
    }

    public bool IsBluetoothAvailable => (int)_centralManager.State == (int)CBCentralManagerState.PoweredOn;

    public async Task<bool> RequestBluetoothPermissions()
    {
        // On iOS, Bluetooth permissions are handled automatically when you
        // try to use CoreBluetooth. The system will prompt the user if needed.
        // We just need to make sure the Info.plist entries are correct.
        await Task.Delay(50); // Small delay to allow state to update if just initialized
        return IsBluetoothAvailable;
    }
}
