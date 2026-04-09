namespace House.Of.Arbitration.Services.Abstractions;

public interface IBluetoothService
{
    bool IsBluetoothAvailable { get; }

    Task<bool> RequestBluetoothPermissions();
}
