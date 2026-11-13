using CommunityToolkit.Mvvm.ComponentModel;

namespace House.Of.Arbitration.Models;

public class DiscoveredDeviceModel : ObservableObject
{
    private string _deviceId = string.Empty;
    private string _name = string.Empty;
    private int _rssi;

    public string DeviceId
    {
        get => _deviceId;
        set
        {
            if (SetProperty(ref _deviceId, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public int Rssi
    {
        get => _rssi;
        set
        {
            if (SetProperty(ref _rssi, value))
            {
                OnPropertyChanged(nameof(SignalIcon));
            }
        }
    }

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
