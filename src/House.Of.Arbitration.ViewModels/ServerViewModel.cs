#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class ServerViewModel : BaseViewModel
{
    #region Services
    private readonly IBluetoothService _bluetoothService;
    private readonly IBluetoothServer _bluetoothServer;
    private readonly IRepository<DrawKnockoutModel> _drawKnockoutService;
    private readonly IRepository<DrawOrderModel> _drawOrderService;
    private readonly IRepository<DrawPoolsModel> _drawPoolsModel;
    #endregion

    #region Attributs
    private string _title = String.Empty;
    private bool _bluetoothAvailable = false;
    private string _serverName = String.Empty;

    private System.Collections.ObjectModel.ObservableCollection<DrawGroup>? _draws;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool BluetoothAvailable
    {
        get => _bluetoothAvailable;
        set => SetProperty(ref _bluetoothAvailable, value);
    }

    public string ServerName
    {
        get => _serverName;
        set => SetProperty(ref _serverName, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<DrawGroup>? Draws
    {
        get => _draws;
        set => SetProperty(ref _draws, value);
    }
    #endregion

    #region Constructors
    public ServerViewModel(
        ILogger<ServerViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IBluetoothService bluetoothService,
        IBluetoothServer bluetoothServer,
        IRepository<DrawKnockoutModel> drawKnockoutService,
        IRepository<DrawOrderModel> drawOrderService,
        IRepository<DrawPoolsModel> drawPoolsService
    ) : base(logger, resourceProvider, popupService)
    {
        Title = "SERVER";

        _bluetoothService = bluetoothService;
        _bluetoothServer = bluetoothServer;

        _drawKnockoutService = drawKnockoutService;
        _drawOrderService = drawOrderService;
        _drawPoolsModel = drawPoolsService;
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        CheckBluetoothAvailabilityCommand.Execute(null);

        var knockouts = (await _drawKnockoutService.GetAllAsync("Draw.Category", "Competitor1", "Competitor2", "Winner", "Looser"))?.ToList();

        var orders = (await _drawOrderService.GetAllAsync("Draw.Category", "Competitor"))?.ToList();

        var pools = (await _drawPoolsModel.GetAllAsync("Draw.Category", "Competitor1","Competitor2","Winner","Looser"))?.ToList();

        var draws = new List<IDrawModel>();

        if (knockouts != null)
        {
            draws.AddRange(knockouts);
        }

        if (orders != null)
        {
            draws.AddRange(orders);
        }

        if (pools != null)
        {
            draws.AddRange(pools);
        }

        var grouped = draws
            .OrderBy(d => d.GlobalOrder)
            .GroupBy(d => d.Draw.Category?.Name ?? "N/A")
            .Select(g => new DrawGroup(g.Key, g.ToList()))
            .ToList();

        Draws = new System.Collections.ObjectModel.ObservableCollection<DrawGroup>(grouped);
    }

    public override Task OnDisappearing()
    {
        return base.OnDisappearing();
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task CheckBluetoothAvailability()
    {
        if (!await _bluetoothService.RequestBluetoothPermissions())
        {
            BluetoothAvailable = false;
        }
        else
        {
            BluetoothAvailable = true;
        }
    }

    [RelayCommand]
    private async Task StartServer()
    {
        await _bluetoothServer.StartAdvertising("BluetoothAppService", ServerName);
    }
    #endregion
}
