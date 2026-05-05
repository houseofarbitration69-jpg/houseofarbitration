namespace House.Of.Arbitration.Models;

public class DiscoveredDeviceModel
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Rssi { get; set; }

    public string DisplayName => string.IsNullOrEmpty(Name) ? DeviceId : Name;

    public string SignalIcon
    {
        get
        {
            if (Rssi > -60) return "󰤨"; // Excellent
            if (Rssi > -70) return "󰤥"; // Good
            if (Rssi > -80) return "󰤢"; // Fair
            return "󰤟"; // Weak
        }
    }
}
