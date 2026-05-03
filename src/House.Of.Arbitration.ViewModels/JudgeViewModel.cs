#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class JudgeViewModel : BaseViewModel
{
    #region Services
    private readonly IBluetoothClient _bluetoothClient;
    private readonly IBluetoothService _bluetoothService;
    #endregion

    #region Attributs
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private JudgeModel? _selectedJudge;

    [ObservableProperty]
    private string? _currentMatchInfo;
    #endregion

    #region Properties
    public ObservableCollection<string> DiscoveredDevices { get; } = new();
    public ObservableCollection<JudgeModel> JudgePositions { get; } = new();
    #endregion

    #region Constructors
    public JudgeViewModel(
        ILogger<JudgeViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IBluetoothClient bluetoothClient,
        IBluetoothService bluetoothService) : base(logger, resourceProvider, popupService)
    {
        _bluetoothClient = bluetoothClient;
        _bluetoothService = bluetoothService;
        _title = "JUDGE";

        for (int i = 1; i <= 5; i++)
        {
            JudgePositions.Add(new JudgeModel { Name = $"JUGE {i}", Number = i });
        }

        _bluetoothClient.DeviceDiscovered += OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected += OnDeviceConnected;
        _bluetoothClient.DeviceDisconnected += OnDeviceDisconnected;
        _bluetoothClient.MessageReceived += OnMessageReceived;
    }
    #endregion

    #region Event Handlers
    private void OnDeviceDiscovered(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!DiscoveredDevices.Contains(deviceId))
                DiscoveredDevices.Add(deviceId);
        });
    }

    private void OnDeviceConnected(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = true;
            IsScanning = false;
        });
    }

    private void OnDeviceDisconnected(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = false;
        });
    }

    private void OnMessageReceived(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentMatchInfo = message;
        });
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task StartScan()
    {
        if (await _bluetoothService.RequestBluetoothPermissions())
        {
            DiscoveredDevices.Clear();
            IsScanning = true;
            await _bluetoothClient.StartScan();
        }
    }

    [RelayCommand]
    private async Task Connect(string deviceId)
    {
        await _bluetoothClient.StopScan();
        IsScanning = false;
        await _bluetoothClient.ConnectToDevice(deviceId);
    }

    [RelayCommand]
    private void SelectPosition(JudgeModel judge)
    {
        SelectedJudge = judge;
    }
    #endregion
}
