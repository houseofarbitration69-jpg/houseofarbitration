using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BluetoothApp.Services;
using System.Collections.ObjectModel;

namespace BluetoothApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothService _bluetoothService;
    private readonly IBluetoothServer _bluetoothServer;
    private readonly IBluetoothClient _bluetoothClient;

    public MainViewModel(IBluetoothService bluetoothService, IBluetoothServer bluetoothServer, IBluetoothClient bluetoothClient)
    {
        _bluetoothService = bluetoothService;
        _bluetoothServer = bluetoothServer;
        _bluetoothClient = bluetoothClient;

        // Initialize event handlers
        _bluetoothServer.MessageReceived += (sender, message) => ServerMessageReceived = message;
        _bluetoothServer.DeviceConnected += (sender, deviceId) => ServerStatus = $"Client Connected: {deviceId}";
        _bluetoothServer.DeviceDisconnected += (sender, deviceId) => ServerStatus = $"Client Disconnected: {deviceId}";

        _bluetoothClient.MessageReceived += (sender, message) => ClientMessageReceived = message;
        _bluetoothClient.DeviceDiscovered += (sender, deviceId) =>
        {
            DiscoveredDevices.Add(deviceId);
        };

        _bluetoothClient.DeviceConnected += (sender, deviceId) => ClientStatus = $"Connected to: {deviceId}";
        _bluetoothClient.DeviceDisconnected += (sender, deviceId) => ClientStatus = $"Disconnected from: {deviceId}";

        ConnectedClients = _bluetoothServer.ConnectedClients;

        CheckBluetoothAvailabilityCommand.Execute(null);
    }

    [ObservableProperty]
    string bluetoothStatus = "Unknown";

    [ObservableProperty]
    string serverStatus = "Idle";

    [ObservableProperty]
    string clientStatus = "Idle";

    [ObservableProperty]
    string serverMessageReceived = "No message";

    [ObservableProperty]
    string clientMessageReceived = "No message";

    [ObservableProperty]
    string messageToSend = "Hello Bluetooth!";

    [ObservableProperty]
    ObservableCollection<string> discoveredDevices = new ObservableCollection<string>();

    [ObservableProperty]
    string selectedDevice = string.Empty;

    [ObservableProperty]
    ObservableCollection<string> connectedClients = new();

    [ObservableProperty]
    string selectedClient = string.Empty;

    [RelayCommand]
    async Task CheckBluetoothAvailability()
    {
        BluetoothStatus = _bluetoothService.IsBluetoothAvailable ? "Available" : "Not Available";
        if (!await _bluetoothService.RequestBluetoothPermissions())
        {
            BluetoothStatus += " (Permissions Denied)";
        }
        else
        {
            BluetoothStatus += " (Permissions Granted)";
        }
    }

    [ObservableProperty]
    string serverName = "MyBluetoothServer";

    [RelayCommand]
    async Task StartServer()
    {
        // Use a generic UUID for demonstration, and provide the custom device name
        await _bluetoothServer.StartAdvertising("BluetoothAppService", ServerName);
        ServerStatus = $"Server Advertising as '{ServerName}'...";
    }

    [RelayCommand]
    async Task StopServer()
    {
        await _bluetoothServer.StopAdvertising();
        ServerStatus = "Server Stopped.";
    }

    [RelayCommand]
    async Task SendBroadcastMessageAsServer()
    {
        await _bluetoothServer.SendToAllAsync(MessageToSend);
    }

    [RelayCommand]
    async Task SendToSelectedClient()
    {
        if (!string.IsNullOrEmpty(SelectedClient))
        {
            await _bluetoothServer.SendToClientAsync(MessageToSend, SelectedClient);
        }
    }

    [RelayCommand]
    async Task StartClientScan()
    {
        DiscoveredDevices.Clear();
        await _bluetoothClient.StartScan();
        ClientStatus = "Client Scanning...";
    }

    [RelayCommand]
    async Task StopClientScan()
    {
        await _bluetoothClient.StopScan();
        ClientStatus = "Client Scan Stopped.";
    }

    [RelayCommand]
    async Task ConnectClientToDevice(string deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            await _bluetoothClient.ConnectToDevice(deviceId);
        }
    }

    [RelayCommand]
    async Task DisconnectClientFromDevice(string deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            await _bluetoothClient.DisconnectFromDevice(deviceId);
        }
    }

    [RelayCommand]
    async Task SendMessageAsClient()
    {
        if (!string.IsNullOrEmpty(SelectedDevice))
        {
            await _bluetoothClient.SendMessage(SelectedDevice, MessageToSend);
        }
    }
}
