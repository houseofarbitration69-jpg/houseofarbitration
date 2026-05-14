#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
#endregion

namespace House.Of.Arbitration.ViewModels;

[QueryProperty(nameof(CompetitionId), "CompetitionId")]
public partial class ServerViewModel : BaseViewModel
{
    #region Services
    private readonly IAlertService _alertService;
    private readonly IBluetoothService _bluetoothService;
    private readonly IBluetoothServer _bluetoothServer;
    private readonly IRepository<CompetitionModel> _competitionRepository;
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
    private bool _isServerStarted;
    private IDispatcherTimer? _timer;

    private int _competitionId;

    private CompetitionModel? _currentCompetition;

    private readonly Dictionary<string, JudgeModel> _clientJudgeMapping = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };
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

    public bool IsServerStarted
    {
        get => _isServerStarted;
        set => SetProperty(ref _isServerStarted, value);
    }

    public IDrawModel? CurrentDraw
    {
        get => _currentDraw;
        set => SetProperty(ref _currentDraw, value);
    }

    public int CompetitionId
    {
        get => _competitionId;
        set => SetProperty(ref _competitionId, value);
    }

    #endregion

    #region Constructors
    public ServerViewModel(
        ILogger<ServerViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IAlertService alertService,
        IBluetoothService bluetoothService,
        IBluetoothServer bluetoothServer,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<DrawKnockoutModel> drawKnockoutService,
        IRepository<DrawOrderModel> drawOrderService,
        IRepository<DrawPoolsModel> drawPoolsService
    ) : base(logger, resourceProvider, popupService)
    {
        Title = "SERVER";

        _alertService = alertService;

        _bluetoothService = bluetoothService;
        _bluetoothServer = bluetoothServer;

        var serverId = _bluetoothServer.InstanceId.ToString().Substring(0, 8);

        _competitionRepository = competitionRepository;
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
            _timer.Tick += async (s, e) =>
            {
                if (_timeLeft.TotalSeconds > 0)
                {
                    _timeLeft = _timeLeft.Subtract(TimeSpan.FromSeconds(1));
                    OnPropertyChanged(nameof(TimeLeftDisplay));
                }
                else
                {
                    StopTimer();
                    await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_STOP);
                }
            };
        }
    }
    #endregion

    #region Event Handlers
    private async void OnDeviceConnected(object? sender, string clientId)
    {
        //var serverId = _bluetoothServer.InstanceId.ToString().Substring(0, 8);

        //if (_currentCompetition != null)
        //{
        //    try
        //    {
        //        var json = JsonSerializer.Serialize(_currentCompetition, _jsonOptions);
        //        await _bluetoothServer.SendToClientAsync($"{Constants.Message.COMPETITION_DATA}{json}", clientId);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error sending competition data to client {ClientId}", clientId);
        //    }
        //}

        //// Send current timer state to the new client
        //await _bluetoothServer.SendToClientAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}", clientId);
        //if (IsTimerRunning)
        //{
        //    await _bluetoothServer.SendToClientAsync(Constants.Message.TIMER_START, clientId);
        //}
    }

    private void OnDeviceDisconnected(object? sender, string clientId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_clientJudgeMapping.TryGetValue(clientId, out var judge))
            {
                judge.IsConnected = false;
                _clientJudgeMapping.Remove(clientId);
            }
        });
    }

    private async void OnMessageReceived(object? sender, (string ClientId, string Message) args)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (args.Message.StartsWith(Constants.Message.JUDGE_POSITION))
            {
                if (int.TryParse(args.Message.Substring(Constants.Message.JUDGE_POSITION.Length), out int position))
                {
                    var judge = Judges.FirstOrDefault(j => j.Number == position);
                    if (judge != null)
                    {
                        // Remove previous mapping for this client if any
                        if (_clientJudgeMapping.TryGetValue(args.ClientId, out var oldJudge))
                        {
                            oldJudge.IsConnected = false;
                        }

                        // Remove mapping for this judge if another client was using it
                        var existingMapping = _clientJudgeMapping.FirstOrDefault(x => x.Value == judge);
                        if (existingMapping.Key != null)
                        {
                            _clientJudgeMapping.Remove(existingMapping.Key);
                        }

                        judge.IsConnected = true;
                        _clientJudgeMapping[args.ClientId] = judge;

                        // Envoie de la compétition
                        if (_currentCompetition != null)
                        {
                            try
                            {
                                var json = JsonSerializer.Serialize(_currentCompetition, _jsonOptions);
                                await _bluetoothServer.SendToClientAsync($"{Constants.Message.COMPETITION_DATA}{json}", args.ClientId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending competition data to client {ClientId}", args.ClientId);
                            }
                        }

                        // Envoi du match courant
                        if (CurrentDraw != null)
                        {
                            try
                            {
                                var json = JsonSerializer.Serialize(new MatchInfoData { Id = CurrentDraw.Id, Type = CurrentDraw.Type }, _jsonOptions);
                                
                                await _bluetoothServer.SendToClientAsync($"{Constants.Message.MATCH_INFO}{json}", args.ClientId);

                                await _bluetoothServer.SendToClientAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}", args.ClientId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending competition data to client {ClientId}", args.ClientId);
                            }
                        }
                    }
                }
            }
            else if(args.Message.StartsWith(Constants.Message.JUDGE_DISCONNECT))
            {
                if (_clientJudgeMapping.TryGetValue(args.ClientId, out var judge))
                {
                    judge.IsConnected = false;
                    _clientJudgeMapping.Remove(args.ClientId);
                }
            }
            else if(args.Message.StartsWith(Constants.Message.JUDGE_SCORE))
            {
                if (_clientJudgeMapping.TryGetValue(args.ClientId, out var judge))
                {
                    try
                    {
                        var json = args.Message.Substring(Constants.Message.JUDGE_SCORE.Length);
                        var scoreData = JsonSerializer.Deserialize<TransfertScoreModel>(json, _jsonOptions);

                        if (scoreData != null && CurrentDraw != null)
                        {
                            var competitor1 = GetCompetitor(CurrentDraw, true);
                            var competitor2 = GetCompetitor(CurrentDraw, false);

                            if (competitor1 != null && scoreData.CompetitorId == competitor1.Id)
                            {
                                judge.RedPoints += scoreData.Score;
                            }
                            else if (competitor2 != null && scoreData.CompetitorId == competitor2.Id)
                            {
                                judge.BluePoints += scoreData.Score;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing judge score from client {ClientId}", args.ClientId);
                    }
                }
            }
        });
    }

    [RelayCommand]
    private async Task BroadcastMatchInfo()
    {
        if (CurrentDraw != null)
        {
            try
            {
                var competitor1 = GetCompetitor(CurrentDraw, true);
                var competitor2 = GetCompetitor(CurrentDraw, false);

                var matchData = new
                {
                    categoryName = CurrentDraw.Draw?.Category?.Name ?? "N/A",
                    currentDrawId = CurrentDraw.Id,
                    competitor1 = competitor1,
                    competitor2 = competitor2,
                    matchNumber = CurrentDraw.GlobalOrder
                };

                var json = JsonSerializer.Serialize(new { Id = CurrentDraw.Id, Type = CurrentDraw.Type}, _jsonOptions);
                await _bluetoothServer.SendToAllAsync($"{Constants.Message.MATCH_INFO}{json}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting match info");
            }
        }
    }

    private CompetitorModel? GetCompetitor(IDrawModel draw, bool red)
    {
        if (draw is DrawKnockoutModel k) return red ? k.Competitor1 : k.Competitor2;
        if (draw is DrawPoolsModel p) return red ? p.Competitor1 : p.Competitor2;
        if (draw is DrawOrderModel o) return o.Competitor;
        return null;
    }

    private string GetCompetitorName(IDrawModel draw, bool red)
    {
        if (draw is DrawKnockoutModel k) return red ? $"{k.Competitor1?.LastName} {k.Competitor1?.FirstName}" : $"{k.Competitor2?.LastName} {k.Competitor2?.FirstName}";
        if (draw is DrawPoolsModel p) return red ? $"{p.Competitor1?.LastName} {p.Competitor1?.FirstName}" : $"{p.Competitor2?.LastName} {p.Competitor2?.FirstName}";
        if (draw is DrawOrderModel o) return o.Competitor?.LastName + " " + o.Competitor?.FirstName;
        return "N/A";
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        _bluetoothServer.DeviceConnected += OnDeviceConnected;
        _bluetoothServer.DeviceDisconnected += OnDeviceDisconnected;
        _bluetoothServer.MessageReceived += OnMessageReceived;

        CheckBluetoothAvailabilityCommand.Execute(null);

        if (CompetitionId > 0)
        {
            _currentCompetition = await _competitionRepository.GetByIdAsync(CompetitionId,
                "Categories.AgeRange",
                "Categories.Competitors.Competitor.Country",
                "Categories.Draw.DrawKnockouts.Competitor1.Country",
                "Categories.Draw.DrawKnockouts.Competitor2.Country",
                "Categories.Draw.DrawKnockouts.Winner",
                "Categories.Draw.DrawKnockouts.Looser",
                "Categories.Draw.DrawOrders.Competitor.Country",
                "Categories.Draw.DrawPools.Competitor1.Country",
                "Categories.Draw.DrawPools.Competitor2.Country",
                "Categories.Draw.DrawPools.Winner",
                "Categories.Draw.DrawPools.Looser");
        }

        var knockouts = (await _drawKnockoutService.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();

        var orders = (await _drawOrderService.GetAllAsync("Draw.Category.AgeRange", "Competitor.Country"))?.ToList();

        var pools = (await _drawPoolsModel.GetAllAsync("Draw.Category.AgeRange", "Competitor1.Country", "Competitor2.Country", "Winner", "Looser"))?.ToList();

        var allDraws = new List<IDrawModel>();

        if (CompetitionId > 0)
        {
            if (knockouts != null) allDraws.AddRange(knockouts.Where(k => k.Draw?.Category?.CompetitionId == CompetitionId));
            if (orders != null) allDraws.AddRange(orders.Where(o => o.Draw?.Category?.CompetitionId == CompetitionId));
            if (pools != null) allDraws.AddRange(pools.Where(p => p.Draw?.Category?.CompetitionId == CompetitionId));
        }
        else
        {
            if (knockouts != null) allDraws.AddRange(knockouts);
            if (orders != null) allDraws.AddRange(orders);
            if (pools != null) allDraws.AddRange(pools);
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

        //await BroadcastMatchInfo();
    }

    public override Task OnDisappearing()
    {
        _bluetoothServer.DeviceConnected -= OnDeviceConnected;
        _bluetoothServer.DeviceDisconnected -= OnDeviceDisconnected;
        _bluetoothServer.MessageReceived -= OnMessageReceived;
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
            await ResetTimer();
            await OnAppearing();
            //await BroadcastMatchInfo();
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
        var result = await _popupService.ShowPopupAsync<ServerSetupPopupViewModel, ServerSetupResult>(Shell.Current);

        if (result.Result != null)
        {
            ServerName = result.Result.Name;
            Title = $"{ServerName} - {result.Result.Description}";
            if (await _bluetoothServer.StartAdvertising("BluetoothAppService", ServerName))
            {
                IsServerStarted = true;
            }
        }
    }

    [RelayCommand]
    private async Task StopServer()
    {
        await _bluetoothServer.StopAdvertising();
        IsServerStarted = false;
        Title = "SERVER";
    }

    [RelayCommand]
    private async Task StartTimer()
    {
        if (!IsTimerRunning && _timeLeft.TotalSeconds > 0)
        {
            _timer?.Start();
            IsTimerRunning = true;
            await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_START);
        }
    }

    [RelayCommand]
    private async Task PauseTimer()
    {
        StopTimer();
        await _bluetoothServer.SendToAllAsync(Constants.Message.TIMER_PAUSE);
    }

    [RelayCommand]
    private async Task ResetTimer()
    {
        StopTimer();
        _timeLeft = TimeSpan.FromMinutes(2); // Default reset
        OnPropertyChanged(nameof(TimeLeftDisplay));
        await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
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
                await _bluetoothServer.SendToAllAsync($"{Constants.Message.TIMER_SET}{TimeLeftDisplay}");
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