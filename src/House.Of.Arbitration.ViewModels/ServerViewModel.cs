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
    private System.Collections.ObjectModel.ObservableCollection<JudgeModel> _judges = new();

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

    public System.Collections.ObjectModel.ObservableCollection<JudgeModel> Judges
    {
        get => _judges;
        set => SetProperty(ref _judges, value);
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

        for (int i = 1; i <= 5; i++)
        {
            Judges.Add(new JudgeModel { Name = $"JUGE {i}", Number = i });
        }

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

        // Start Bluetooth Server automatically
        if (BluetoothAvailable)
        {
            await StartServer();
        }

        var knockouts = (await _drawKnockoutService.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();

        var orders = (await _drawOrderService.GetAllAsync("Draw.Category.AgeRange", "Competitor.Country"))?.ToList();

        var pools = (await _drawPoolsModel.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();

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

        foreach (var draw in sortedDraws.Where(d => !d.IsFinished))
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
    private async Task SetWinner(string color)
    {
        if (CurrentDraw == null) return;

        CompetitorModel? winner = null;
        CompetitorModel? looser = null;

        if (CurrentDraw is DrawKnockoutModel knockout)
        {
            winner = color == "Red" ? knockout.Competitor1 : knockout.Competitor2;
            looser = color == "Red" ? knockout.Competitor2 : knockout.Competitor1;

            if (winner == null) return;

            knockout.WinnerId = winner.Id;
            knockout.LooserId = looser?.Id;
            knockout.IsFinished = true;
            await _drawKnockoutService.UpdateAsync(knockout);

            // Propagation logic for Knockouts
            var allKnockouts = (await _drawKnockoutService.GetAllAsync()).Where(k => k.DrawId == knockout.DrawId).OrderBy(k => k.Order).ToList();
            if (allKnockouts.Count > 0)
            {
                // Determine 'm' (power of 2 bracket size)
                // Total matches = m - 1
                int m = allKnockouts.Max(k => k.Order) + 1;

                // Find current round and local index
                int currentOrder = knockout.Order;
                int roundStart = 0;
                int matchesInRound = m / 2;

                while (matchesInRound > 0)
                {
                    if (currentOrder > roundStart && currentOrder <= roundStart + matchesInRound)
                    {
                        // Found current round
                        int localIndex = currentOrder - roundStart; // 1-indexed
                        int nextRoundStart = roundStart + matchesInRound;
                        int nextMatchesInRound = matchesInRound / 2;

                        if (nextMatchesInRound > 0)
                        {
                            int nextMatchOrder = nextRoundStart + (localIndex + 1) / 2;
                            var nextMatch = allKnockouts.FirstOrDefault(k => k.Order == nextMatchOrder);

                            if (nextMatch != null)
                            {
                                if (localIndex % 2 != 0) // Odd index -> Slot 1
                                {
                                    nextMatch.Competitor1Id = winner.Id;
                                }
                                else // Even index -> Slot 2
                                {
                                    nextMatch.Competitor2Id = winner.Id;
                                }
                                await _drawKnockoutService.UpdateAsync(nextMatch);
                            }
                        }
                        break;
                    }

                    roundStart += matchesInRound;
                    matchesInRound /= 2;
                }
            }
        }
        else if (CurrentDraw is DrawPoolsModel pool)
        {
            winner = color == "Red" ? pool.Competitor1 : pool.Competitor2;
            looser = color == "Red" ? pool.Competitor2 : pool.Competitor1;

            if (winner == null) return;

            pool.WinnerId = winner.Id;
            pool.LooserId = looser?.Id;
            pool.IsFinished = true;
            await _drawPoolsModel.UpdateAsync(pool);
        }
        else if (CurrentDraw is DrawOrderModel order)
        {
            order.IsFinished = true;
            await _drawOrderService.UpdateAsync(order);
            winner = order.Competitor; // Just to trigger next
        }

        if (winner != null)
        {
            // Reset judge points
            foreach (var judge in Judges)
            {
                judge.RedPoints = 0;
                judge.BluePoints = 0;
            }

            // Reset timer and load next
            ResetTimer();
            await OnAppearing();
        }
    }

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

    [RelayCommand]
    private async Task OpenJudgePopup(JudgeModel? judge)
    {
        var parameters = new Dictionary<string, object>
        {
            { "Judge", judge }
        };

        var result = await _popupService.ShowPopupAsync<JudgePointsPopupViewModel, JudgeModel>(Shell.Current, shellParameters: parameters);

        if (result.Result != null)
        {
            judge.RedPoints = result.Result.RedPoints;
            judge.BluePoints = result.Result.BluePoints;
        }
    }

    [RelayCommand]
    private void AddRedPoint(JudgeModel judge)
    {
        judge.RedPoints++;
    }

    [RelayCommand]
    private void RemoveRedPoint(JudgeModel judge)
    {
        if (judge.RedPoints > 0)
            judge.RedPoints--;
    }

    [RelayCommand]
    private void AddBluePoint(JudgeModel judge)
    {
        judge.BluePoints++;
    }

    [RelayCommand]
    private void RemoveBluePoint(JudgeModel judge)
    {
        if (judge.BluePoints > 0)
            judge.BluePoints--;
    }
    #endregion
}