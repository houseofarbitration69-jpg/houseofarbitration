namespace BluetoothApp.Services;

public interface IBluetoothService
{
    // Common Bluetooth operations, e.g., checking adapter status, requesting permissions
    bool IsBluetoothAvailable { get; }
    Task<bool> RequestBluetoothPermissions();
}
