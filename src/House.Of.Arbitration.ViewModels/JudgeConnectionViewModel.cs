#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class JudgeConnectionViewModel : BaseViewModel
{
    #region Services
    /// <summary>
    /// Service de gestion des alertes
    /// </summary>
    private readonly IAlertService _alertService;

    /// <summary>
    /// Service de gestion du bluetooth
    /// </summary>
    private readonly IBluetoothService _bluetoothService;

    /// <summary>
    /// Service pour le client bluetooth
    /// </summary>
    private readonly IBluetoothClient _bluetoothClient;

    /// <summary>
    /// Service de gestion de la base de données
    /// </summary>
    private readonly IDatabaseService _databaseService;
    #endregion

    #region Attributs
    private bool _isScanning;
    private bool _isReceivingCompetition;
    private string _serverId = String.Empty;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true
    };
    #endregion

    #region Properties
    /// <summary>
    /// Obtient la liste des devices découvert
    /// </summary>
    public ObservableCollection<DiscoveredDeviceModel> DiscoveredDevices { get; } = new();

    /// <summary>
    /// Obtient ou définit si le scan est en court
    /// </summary>
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    /// <summary>
    /// Obtient ou définit si on est encours de récupération d'une compétition
    /// </summary>
    public bool IsReceivingCompetition
    {
        get => _isReceivingCompetition;
        set => SetProperty(ref _isReceivingCompetition, value);
    }

    /// <summary>
    /// Obtient ou définit l'identifiant du serveur
    /// </summary>
    public string ServerId
    {
        get => _serverId;
        set => SetProperty(ref _serverId, value);
    }
    #endregion

    #region Constructors
    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="resourceProvider"></param>
    /// <param name="popupService"></param>
    /// <param name="alertService"></param>
    /// <param name="bluetoothClient"></param>
    /// <param name="bluetoothService"></param>
    public JudgeConnectionViewModel(
        ILogger<JudgePositionViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IAlertService alertService,
        IBluetoothClient bluetoothClient,
        IBluetoothService bluetoothService,
        IDatabaseService databaseService
    ) : base(logger, resourceProvider, popupService)
    {
        _alertService = alertService;

        _bluetoothClient = bluetoothClient;
        _bluetoothService = bluetoothService;

        _databaseService = databaseService;
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override async Task OnAppearing()
    {
        _bluetoothClient.DeviceDiscovered += OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected += OnDeviceConnected;
        _bluetoothClient.MessageReceived += OnMessageReceived;

        await base.OnAppearing();
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override async Task OnDisappearing()
    {
        _bluetoothClient.DeviceDiscovered -= OnDeviceDiscovered;
        _bluetoothClient.DeviceConnected -= OnDeviceConnected;
        _bluetoothClient.MessageReceived -= OnMessageReceived;

        IsReceivingCompetition = false;

        await base.OnDisappearing();
    }
    #endregion

    #region Commands
    /// <summary>
    /// Commande permettant de récupérer la liste des serveurs présent
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// COmmande permettant de se connecter au serveur
    /// </summary>
    /// <param name="device"></param>
    /// <returns></returns>
    [RelayCommand]
    private async Task Connect(DiscoveredDeviceModel? device)
    {
        if (device != null)
        {
            await _bluetoothClient.StopScan();
            IsScanning = false;
            IsReceivingCompetition = true;
            ServerId = device.DeviceId;
            await _bluetoothClient.ConnectToDevice(device.DeviceId);
        }
    }
    #endregion

    #region Events
    /// <summary>
    /// Méthode appellée pour découvrir les serveur possible
    /// </summary>
    private void OnDeviceDiscovered(object? sender, (string DeviceId, string Name, int Rssi) device)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = DiscoveredDevices.FirstOrDefault(d => d.Name == device.Name);
            if (existing != null)
            {
                existing.DeviceId = device.DeviceId;
                existing.Rssi = device.Rssi;
            }
            else
            {
                DiscoveredDevices.Add(new DiscoveredDeviceModel
                {
                    DeviceId = device.DeviceId,
                    Name = device.Name,
                    Rssi = device.Rssi
                });
            }
        });
    }

    /// <summary>
    /// Méthode appellée lorsqu'on se connect sur un serveur
    /// </summary>
    public void OnDeviceConnected(object? sender, string deviceId)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleDeviceConnectedAsync(deviceId);
        });
    }

    /// <summary>
    /// Méthode appellée lorsqu'on recoit un message
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    public void OnMessageReceived(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleMessageAsync(message);
        });
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 
    /// </summary>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    private async Task HandleDeviceConnectedAsync(string deviceId)
    {
        IsReceivingCompetition = true;
        try
        {
            await _bluetoothClient.SendMessage(deviceId, Constants.Message.GET_COMPETITION);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting competition from server");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task HandleMessageAsync(string message)
    {
        try
        {
            if (message.StartsWith(Constants.Message.COMPETITION_DATA))
            {
                var content = message.Substring(Constants.Message.COMPETITION_DATA.Length);
                var competition = JsonSerializer.Deserialize<CompetitionModel>(content, _jsonOptions);
                if (competition != null)
                {
                    if (_databaseService != null)
                    {
                        await _databaseService.ImportCompetitionDataAsync(competition);

                        var navigationParameter = new Dictionary<string, object>
                        {
                            { Constants.Navigation.Parameter.SERVER_ID, ServerId },
                            { Constants.Navigation.Parameter.COMPETITION_ID, competition.Id },
                        };

                        await Shell.Current.GoToAsync($"/{Constants.Navigation.Page.JUDGE_POSITION}", navigationParameter);
                    }
                }
                IsReceivingCompetition = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received message");
            await _alertService.ShowToast($"Error: {ex.Message}");
        }
    }
    #endregion
}
