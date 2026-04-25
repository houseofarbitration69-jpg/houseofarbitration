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

    private System.Collections.ObjectModel.ObservableCollection<object>? _draws;
    private IDrawModel? _currentDraw;

    private TimeSpan _timeLeft = TimeSpan.FromMinutes(2);
    private bool _isTimerRunning;
    private IDispatcherTimer? _timer;
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

    public System.Collections.ObjectModel.ObservableCollection<object>? Draws
    {
        get => _draws;
        set => SetProperty(ref _draws, value);
    }

    public string TimeLeftDisplay => _timeLeft.ToString(@"mm\:ss");

    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        set => SetProperty(ref _isTimerRunning, value);
    }

    public IDrawModel? CurrentDraw
    {
        get => _currentDraw;
        set => SetProperty(ref _currentDraw, value);
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

        _timer = Application.Current?.Dispatcher.CreateTimer();
        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                if (_timeLeft.TotalSeconds > 0)
                {
                    _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
                    OnPropertyChanged(nameof(TimeLeftDisplay));
                }
                else
                {
                    StopTimer();
                }
            };
        }
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        CheckBluetoothAvailabilityCommand.Execute(null);

        var knockouts = (await _drawKnockoutService.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();

        var orders = (await _drawOrderService.GetAllAsync("Draw.Category.AgeRange", "Competitor.Country"))?.ToList();

        var pools = (await _drawPoolsModel.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country","Competitor2.Country","Winner","Looser"))?.ToList();

        var allDraws = new List<IDrawModel>();

        if (knockouts != null)
        {
            allDraws.AddRange(knockouts);
        }

        if (orders != null)
        {
            allDraws.AddRange(orders);
        }

        if (pools != null)
        {
            allDraws.AddRange(pools);
        }

        var sortedDraws = allDraws.OrderBy(d => d.GlobalOrder).ToList();

        CurrentDraw = sortedDraws.FirstOrDefault(d => !d.IsFinished);
        
        var flattenedList = new List<object>();
        string? lastCategoryName = null;

        foreach (var draw in sortedDraws)
        {
            var currentCategoryName = draw.Draw.Category?.Name ?? "N/A";
            if (currentCategoryName != lastCategoryName)
            {
                flattenedList.Add(currentCategoryName);
                lastCategoryName = currentCategoryName;
            }
            flattenedList.Add(draw);
        }

        Draws = new System.Collections.ObjectModel.ObservableCollection<object>(flattenedList);
    }

    public override Task OnDisappearing()
    {
        return base.OnDisappearing();
    }
    #endregion

    #region Private Methods
    private void StopTimer()
    {
        _timer?.Stop();
        IsTimerRunning = false;
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

    [RelayCommand]
    private void StartTimer()
    {
        if (!IsTimerRunning && _timeLeft.TotalSeconds > 0)
        {
            _timer?.Start();
            IsTimerRunning = true;
        }
    }

    [RelayCommand]
    private void PauseTimer()
    {
        StopTimer();
    }

    [RelayCommand]
    private void ResetTimer()
    {
        StopTimer();
        _timeLeft = TimeSpan.FromMinutes(2); // Default reset
        OnPropertyChanged(nameof(TimeLeftDisplay));
    }

    [RelayCommand]
    private async Task SetTimer()
    {
        string result = await Shell.Current.DisplayActionSheet("Définir le temps", "Annuler", null, "1:00", "1:30", "2:00", "3:00", "5:00");
        if (result != null && result != "Annuler")
        {
            StopTimer();
            if (TimeSpan.TryParseExact(result, @"m\:ss", null, out var newTime))
            {
                _timeLeft = newTime;
                OnPropertyChanged(nameof(TimeLeftDisplay));
            }
        }
    }
    #endregion
}