#region Imports
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

[QueryProperty(nameof(CompetitionId), Constants.Navigation.Parameter.COMPETITION_ID)]
[QueryProperty(nameof(CurrentCategoryId), Constants.Navigation.Parameter.CATEGORY_ID)]
public partial class ServerDrawsViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IRepository<CompetitionModel> _competitionRepository;
    private readonly IRepository<CategoryModel> _categoryRepository;
    private readonly IRepository<DrawModel> _drawRepository;
    private readonly IRepository<DrawKnockoutModel> _drawKnockoutRepository;
    private readonly IRepository<DrawPoolsModel> _drawPoolsRepository;
    private readonly IRepository<DrawOrderModel> _drawOrderRepository;
    private readonly IRepository<RefereeDataModel> _refereeDataRepository;
    private readonly IAlertService _alertService;
    #endregion

    #region Fields
    private int _competitionId;
    private int _currentCategoryId;
    private string _competitionName = string.Empty;
    private ObservableCollection<CategoryModel> _categories = new();
    private CategoryModel? _selectedCategory;
    private DrawModel? _currentDrawModel;

    private ObservableCollection<ServerKnockoutRoundItem> _knockoutRounds = new();
    private ObservableCollection<ServerKnockoutMatchItem> _bronzeMatches = new();
    private ObservableCollection<ServerPoolMatchItem> _poolMatches = new();
    private ObservableCollection<ServerPoolStandingItem> _poolStandings = new();
    private ObservableCollection<ServerOrderItem> _orderItems = new();
    private string _taoluWinnerName = string.Empty;
    private ServerKnockoutSlotItem? _draggedSlot;
    private bool _isDragging;
    private bool _isLoadingCategories;
    #endregion

    #region Properties
    public int CompetitionId
    {
        get => _competitionId;
        set => SetProperty(ref _competitionId, value);
    }

    public int CurrentCategoryId
    {
        get => _currentCategoryId;
        set => SetProperty(ref _currentCategoryId, value);
    }

    public string CompetitionName
    {
        get => _competitionName;
        set => SetProperty(ref _competitionName, value);
    }

    public ObservableCollection<CategoryModel> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public CategoryModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(IsKnockouts));
                OnPropertyChanged(nameof(IsPools));
                OnPropertyChanged(nameof(IsOrder));
                _ = LoadCategoryDrawAsync();
            }
        }
    }

    public bool IsKnockouts => SelectedCategory?.RoundType == RoundType.Knockouts;
    public bool IsPools => SelectedCategory?.RoundType == RoundType.Pools;
    public bool IsOrder => SelectedCategory?.RoundType == RoundType.Order;

    public ObservableCollection<ServerKnockoutRoundItem> KnockoutRounds
    {
        get => _knockoutRounds;
        set => SetProperty(ref _knockoutRounds, value);
    }

    public ObservableCollection<ServerKnockoutMatchItem> BronzeMatches
    {
        get => _bronzeMatches;
        set
        {
            if (SetProperty(ref _bronzeMatches, value))
            {
                OnPropertyChanged(nameof(HasBronzeMatch));
            }
        }
    }

    public bool HasBronzeMatch => BronzeMatches.Count > 0;

    public ObservableCollection<ServerPoolMatchItem> PoolMatches
    {
        get => _poolMatches;
        set => SetProperty(ref _poolMatches, value);
    }

    public ObservableCollection<ServerPoolStandingItem> PoolStandings
    {
        get => _poolStandings;
        set => SetProperty(ref _poolStandings, value);
    }

    public ObservableCollection<ServerOrderItem> OrderItems
    {
        get => _orderItems;
        set => SetProperty(ref _orderItems, value);
    }

    public string TaoluWinnerName
    {
        get => _taoluWinnerName;
        set => SetProperty(ref _taoluWinnerName, value);
    }

    public ServerKnockoutSlotItem? DraggedSlot
    {
        get => _draggedSlot;
        set => SetProperty(ref _draggedSlot, value);
    }

    public bool IsDragging
    {
        get => _isDragging;
        set => SetProperty(ref _isDragging, value);
    }
    #endregion

    #region Constructor
    public ServerDrawsViewModel(
        ILogger<ServerDrawsViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository,
        IRepository<DrawModel> drawRepository,
        IRepository<DrawKnockoutModel> drawKnockoutRepository,
        IRepository<DrawPoolsModel> drawPoolsRepository,
        IRepository<DrawOrderModel> drawOrderRepository,
        IRepository<RefereeDataModel> refereeDataRepository,
        IAlertService alertService) : base(logger, resourceProvider, popupService)
    {
        _competitionRepository = competitionRepository;
        _categoryRepository = categoryRepository;
        _drawRepository = drawRepository;
        _drawKnockoutRepository = drawKnockoutRepository;
        _drawPoolsRepository = drawPoolsRepository;
        _drawOrderRepository = drawOrderRepository;
        _refereeDataRepository = refereeDataRepository;
        _alertService = alertService;
    }
    #endregion

    #region Navigation
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Constants.Navigation.Parameter.COMPETITION_ID, out var compObj) &&
            int.TryParse(compObj?.ToString(), out int compId))
        {
            CompetitionId = compId;
        }

        if (query.TryGetValue(Constants.Navigation.Parameter.CATEGORY_ID, out var catObj) &&
            int.TryParse(catObj?.ToString(), out int catId))
        {
            CurrentCategoryId = catId;
        }

        if (Categories.Count == 0 && CompetitionId > 0)
        {
            MainThread.BeginInvokeOnMainThread(async () => await LoadCategoriesAsync());
        }
    }

    public override async Task OnAppearing()
    {
        await base.OnAppearing();
        if (Categories.Count == 0)
        {
            await LoadCategoriesAsync();
        }
    }

    [RelayCommand]
    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
    }
    #endregion

    #region Load Data
    public async Task LoadCategoriesAsync()
    {
        if (_isLoadingCategories) return;
        _isLoadingCategories = true;
        try
        {
            IsBusy = true;
            if (CompetitionId > 0)
            {
                var comp = await _competitionRepository.GetByIdAsync(CompetitionId);
                if (comp != null) CompetitionName = comp.Name;
            }

            var allCats = await _categoryRepository.GetAllAsync("AgeRange");
            var filtered = allCats
                .Where(c => CompetitionId <= 0 || c.CompetitionId == CompetitionId)
                .OrderBy(c => c.Id)
                .ToList();

            Categories = new ObservableCollection<CategoryModel>(filtered);

            var defaultCat = filtered.FirstOrDefault(c => c.Id == CurrentCategoryId) ?? filtered.FirstOrDefault();
            SelectedCategory = defaultCat;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur chargement catégories dans ServerDrawsViewModel");
        }
        finally
        {
            _isLoadingCategories = false;
            IsBusy = false;
        }
    }

    public async Task LoadCategoryDrawAsync()
    {
        KnockoutRounds = new();
        BronzeMatches = new();
        PoolMatches = new();
        PoolStandings = new();
        OrderItems = new();
        TaoluWinnerName = string.Empty;

        if (SelectedCategory == null) return;

        try
        {
            var allDraws = await _drawRepository.GetAllAsync(
                "DrawKnockouts.Competitor1",
                "DrawKnockouts.Competitor2",
                "DrawKnockouts.Winner",
                "DrawKnockouts.RefereeDatas",
                "DrawPools.Competitor1",
                "DrawPools.Competitor2",
                "DrawPools.Winner",
                "DrawPools.RefereeDatas",
                "DrawOrders.Competitor",
                "DrawOrders.RefereeDatas");

            _currentDrawModel = allDraws.FirstOrDefault(d => d.CategoryId == SelectedCategory.Id);
            if (_currentDrawModel == null) return;

            if (IsKnockouts)
            {
                BuildKnockoutRounds(_currentDrawModel.DrawKnockouts.OrderBy(k => k.Order).ToList());
            }
            else if (IsPools)
            {
                BuildPoolMatches(_currentDrawModel.DrawPools.OrderBy(p => p.Order).ToList());
            }
            else if (IsOrder)
            {
                var orders = _currentDrawModel.DrawOrders.OrderBy(o => o.Order).ToList();
                var allRefDatas = await _refereeDataRepository.GetAllAsync();
                var refDatasByOrder = (allRefDatas ?? Array.Empty<RefereeDataModel>())
                    .Where(r => r.DrawOrderId.HasValue)
                    .GroupBy(r => r.DrawOrderId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                BuildOrderItems(orders, refDatasByOrder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur chargement tirage catégorie #{CategoryId}", SelectedCategory.Id);
        }
    }
    #endregion

    #region Knockouts (Éliminatoires + Drag'n'Drop + Propagation)
    private void BuildKnockoutRounds(List<DrawKnockoutModel> knockouts)
    {
        var rounds = new ObservableCollection<ServerKnockoutRoundItem>();
        if (knockouts.Count == 0)
        {
            BronzeMatches = new();
            KnockoutRounds = rounds;
            return;
        }

        var bronzeModel = knockouts.FirstOrDefault(k => k.IsBronzeMatch);
        BronzeMatches = bronzeModel != null
            ? new ObservableCollection<ServerKnockoutMatchItem> { CreateKnockoutMatchItem(bronzeModel, "Petite finale") }
            : new();

        var mainKnockouts = knockouts.Where(k => !k.IsBronzeMatch).OrderBy(k => k.Order).ToList();
        int mainCount = mainKnockouts.Count;
        int m = mainCount + 1;
        int roundStart = 0;
        int matchesInRound = m / 2;
        int roundNumber = 1;
        const double baseSlotHeight = 180.0;

        while (matchesInRound > 0)
        {
            var roundMatches = mainKnockouts
                .Where(k => matchesInRound == 1
                    ? k.Order > roundStart
                    : (k.Order > roundStart && k.Order <= roundStart + matchesInRound))
                .ToList();

            if (roundMatches.Count > 0)
            {
                string roundName = matchesInRound switch
                {
                    1 => "Finale",
                    2 => "1/2 finale",
                    4 => "1/4 finale",
                    8 => "1/8 finale",
                    _ => $"Tour {roundNumber}"
                };

                double slotHeight = baseSlotHeight * Math.Pow(2, roundNumber - 1);
                bool hasNextRound = matchesInRound > 1;
                var roundVm = new ServerKnockoutRoundItem { Name = roundName };

                for (int i = 0; i < roundMatches.Count; i++)
                {
                    var item = CreateKnockoutMatchItem(roundMatches[i], roundName);
                    item.BracketSlotHeight = slotHeight;
                    item.ShowLeftConnector = roundNumber > 1;
                    item.ShowRightConnector = hasNextRound;
                    item.IsTopFeeder = i % 2 == 0;
                    roundVm.Matches.Add(item);
                }
                rounds.Add(roundVm);
            }

            roundStart += matchesInRound;
            matchesInRound /= 2;
            roundNumber++;
        }

        KnockoutRounds = rounds;
    }

    private ServerKnockoutMatchItem CreateKnockoutMatchItem(DrawKnockoutModel model, string stageLabel)
    {
        var item = new ServerKnockoutMatchItem
        {
            Model = model,
            StageLabel = $"{stageLabel} (#{model.Order})",
            IsFinished = model.IsFinished,
            CanReset = model.IsFinished || model.CurrentRound > 1 || !string.IsNullOrWhiteSpace(model.RoundResultsJson) || (model.RefereeDatas?.Count > 0),
            IsBye = model.IsBye,
            WinnerName = model.Winner?.Name ?? string.Empty
        };

        item.Slot1 = new ServerKnockoutSlotItem
        {
            MatchItem = item,
            SlotNumber = 1,
            Competitor = model.Competitor1,
            IsWinner = model.WinnerId.HasValue && model.Competitor1Id == model.WinnerId,
            IsLoser = model.LooserId.HasValue && model.Competitor1Id == model.LooserId
        };

        item.Slot2 = new ServerKnockoutSlotItem
        {
            MatchItem = item,
            SlotNumber = 2,
            Competitor = model.Competitor2,
            IsWinner = model.WinnerId.HasValue && model.Competitor2Id == model.WinnerId,
            IsLoser = model.LooserId.HasValue && model.Competitor2Id == model.LooserId
        };

        return item;
    }

    [RelayCommand]
    private void DragStarting(ServerKnockoutSlotItem? slot)
    {
        if (slot == null || slot.Competitor == null) return;
        DraggedSlot = slot;
        IsDragging = true;
    }

    [RelayCommand]
    private void DragCompleted()
    {
        IsDragging = false;
        DraggedSlot = null;
    }

    [RelayCommand]
    public async Task DropOnSlot(ServerKnockoutSlotItem? targetSlot)
    {
        var sourceSlot = DraggedSlot;
        IsDragging = false;
        DraggedSlot = null;

        if (sourceSlot == null || targetSlot == null || sourceSlot == targetSlot)
            return;

        await SwapKnockoutSlotsAsync(sourceSlot, targetSlot);
    }

    public async Task SwapKnockoutSlotsAsync(ServerKnockoutSlotItem sourceSlot, ServerKnockoutSlotItem targetSlot)
    {
        if (_currentDrawModel == null) return;

        var allKnockouts = _currentDrawModel.DrawKnockouts.OrderBy(k => k.Order).ToList();
        var srcMatch = sourceSlot.MatchItem.Model;
        var tgtMatch = targetSlot.MatchItem.Model;

        var srcComp = sourceSlot.SlotNumber == 1 ? srcMatch.Competitor1 : srcMatch.Competitor2;
        var srcCompId = sourceSlot.SlotNumber == 1 ? srcMatch.Competitor1Id : srcMatch.Competitor2Id;
        var tgtComp = targetSlot.SlotNumber == 1 ? tgtMatch.Competitor1 : tgtMatch.Competitor2;
        var tgtCompId = targetSlot.SlotNumber == 1 ? tgtMatch.Competitor1Id : tgtMatch.Competitor2Id;

        if (sourceSlot.SlotNumber == 1)
        {
            srcMatch.Competitor1 = tgtComp;
            srcMatch.Competitor1Id = tgtCompId;
        }
        else
        {
            srcMatch.Competitor2 = tgtComp;
            srcMatch.Competitor2Id = tgtCompId;
        }

        if (targetSlot.SlotNumber == 1)
        {
            tgtMatch.Competitor1 = srcComp;
            tgtMatch.Competitor1Id = srcCompId;
        }
        else
        {
            tgtMatch.Competitor2 = srcComp;
            tgtMatch.Competitor2Id = srcCompId;
        }

        // Réinitialiser les matchs affectés et nettoyer en cascade les tours suivants
        await ClearMatchOutcomeAndCascadeAsync(srcMatch, allKnockouts);
        if (tgtMatch.Id != srcMatch.Id)
        {
            await ClearMatchOutcomeAndCascadeAsync(tgtMatch, allKnockouts);
        }

        BuildKnockoutRounds(allKnockouts);
    }

    [RelayCommand]
    public async Task SetKnockoutWinner(ServerKnockoutSlotItem? slot)
    {
        if (slot == null || slot.Competitor == null || _currentDrawModel == null) return;

        var match = slot.MatchItem.Model;
        var winner = slot.Competitor;
        var looser = slot.SlotNumber == 1 ? match.Competitor2 : match.Competitor1;
        var allKnockouts = _currentDrawModel.DrawKnockouts.OrderBy(k => k.Order).ToList();

        // Si le vainqueur change sur un match déjà terminé, nettoyer d'abord l'ancienne propagation en cascade
        if (match.WinnerId.HasValue && match.WinnerId.Value != winner.Id)
        {
            await ClearMatchOutcomeAndCascadeAsync(match, allKnockouts);
        }

        match.WinnerId = winner.Id;
        match.Winner = winner;
        match.LooserId = looser?.Id;
        match.Looser = looser;
        match.IsFinished = true;
        await _drawKnockoutRepository.UpdateAsync(ToDetachedKnockout(match));

        await PropagateKnockoutOutcomeAsync(match, winner, looser, allKnockouts);
        BuildKnockoutRounds(allKnockouts);
    }

    [RelayCommand]
    public async Task ResetKnockoutMatch(ServerKnockoutMatchItem? matchItem)
    {
        if (matchItem == null || _currentDrawModel == null) return;

        var allKnockouts = _currentDrawModel.DrawKnockouts.OrderBy(k => k.Order).ToList();
        await ClearMatchOutcomeAndCascadeAsync(matchItem.Model, allKnockouts);
        BuildKnockoutRounds(allKnockouts);
    }

    private async Task ClearMatchOutcomeAndCascadeAsync(DrawKnockoutModel match, List<DrawKnockoutModel> allKnockouts)
    {
        int? previousWinnerId = match.WinnerId;
        int? previousLooserId = match.LooserId;

        match.WinnerId = null;
        match.Winner = null;
        match.LooserId = null;
        match.Looser = null;
        match.IsFinished = false;
        match.CurrentRound = 1;
        match.RoundResultsJson = string.Empty;
        match.RefereeDatas?.Clear();
        await _drawKnockoutRepository.UpdateAsync(ToDetachedKnockout(match));
        await DeleteRefereeDatasForMatchAsync(drawKnockoutId: match.Id);

        if (!previousWinnerId.HasValue && !previousLooserId.HasValue)
            return;

        var (nextMatch, bronzeMatch, localIndex) = FindDownstreamKnockoutMatches(match, allKnockouts);

        if (nextMatch != null)
        {
            bool targetIsSlot1 = localIndex % 2 != 0;
            bool changed = false;

            if (targetIsSlot1 && nextMatch.Competitor1Id == previousWinnerId)
            {
                nextMatch.Competitor1Id = null;
                nextMatch.Competitor1 = null;
                changed = true;
            }
            else if (!targetIsSlot1 && nextMatch.Competitor2Id == previousWinnerId)
            {
                nextMatch.Competitor2Id = null;
                nextMatch.Competitor2 = null;
                changed = true;
            }

            if (changed)
            {
                await ClearMatchOutcomeAndCascadeAsync(nextMatch, allKnockouts);
            }
        }

        if (bronzeMatch != null && previousLooserId.HasValue)
        {
            bool targetIsSlot1 = localIndex % 2 != 0;
            bool changed = false;

            if (targetIsSlot1 && bronzeMatch.Competitor1Id == previousLooserId)
            {
                bronzeMatch.Competitor1Id = null;
                bronzeMatch.Competitor1 = null;
                changed = true;
            }
            else if (!targetIsSlot1 && bronzeMatch.Competitor2Id == previousLooserId)
            {
                bronzeMatch.Competitor2Id = null;
                bronzeMatch.Competitor2 = null;
                changed = true;
            }

            if (changed)
            {
                await ClearMatchOutcomeAndCascadeAsync(bronzeMatch, allKnockouts);
            }
        }
    }

    private async Task PropagateKnockoutOutcomeAsync(
        DrawKnockoutModel match,
        CompetitorModel winner,
        CompetitorModel? looser,
        List<DrawKnockoutModel> allKnockouts)
    {
        var (nextMatch, bronzeMatch, localIndex) = FindDownstreamKnockoutMatches(match, allKnockouts);

        if (nextMatch != null)
        {
            bool targetIsSlot1 = localIndex % 2 != 0;
            if (targetIsSlot1)
            {
                nextMatch.Competitor1Id = winner.Id;
                nextMatch.Competitor1 = winner;
            }
            else
            {
                nextMatch.Competitor2Id = winner.Id;
                nextMatch.Competitor2 = winner;
            }
            await _drawKnockoutRepository.UpdateAsync(ToDetachedKnockout(nextMatch));
        }

        if (bronzeMatch != null && looser != null)
        {
            bool targetIsSlot1 = localIndex % 2 != 0;
            if (targetIsSlot1)
            {
                bronzeMatch.Competitor1Id = looser.Id;
                bronzeMatch.Competitor1 = looser;
            }
            else
            {
                bronzeMatch.Competitor2Id = looser.Id;
                bronzeMatch.Competitor2 = looser;
            }
            await _drawKnockoutRepository.UpdateAsync(ToDetachedKnockout(bronzeMatch));
        }
    }

    private static (DrawKnockoutModel? NextMatch, DrawKnockoutModel? BronzeMatch, int LocalIndex) FindDownstreamKnockoutMatches(
        DrawKnockoutModel match,
        List<DrawKnockoutModel> allKnockouts)
    {
        if (allKnockouts.Count == 0 || match.IsBronzeMatch)
            return (null, null, 0);

        int m = allKnockouts.Count(k => !k.IsBronzeMatch) + 1;
        int currentOrder = match.Order;
        int roundStart = 0;
        int matchesInRound = m / 2;

        while (matchesInRound > 0)
        {
            if (currentOrder > roundStart && currentOrder <= roundStart + matchesInRound)
            {
                int localIndex = currentOrder - roundStart;
                int nextRoundStart = roundStart + matchesInRound;
                int nextMatchesInRound = matchesInRound / 2;

                if (nextMatchesInRound > 0)
                {
                    DrawKnockoutModel? nextMatch = matchesInRound == 2
                        ? allKnockouts.LastOrDefault(k => !k.IsBronzeMatch)
                        : allKnockouts.FirstOrDefault(k => k.Order == nextRoundStart + (localIndex + 1) / 2);

                    DrawKnockoutModel? bronzeMatch = matchesInRound == 2
                        ? allKnockouts.FirstOrDefault(k => k.IsBronzeMatch)
                        : null;

                    return (nextMatch, bronzeMatch, localIndex);
                }
                return (null, null, localIndex);
            }
            roundStart += matchesInRound;
            matchesInRound /= 2;
        }

        return (null, null, 0);
    }

    private static DrawKnockoutModel ToDetachedKnockout(DrawKnockoutModel k) => new()
    {
        Id = k.Id,
        DrawId = k.DrawId,
        Order = k.Order,
        IsBronzeMatch = k.IsBronzeMatch,
        CurrentRound = k.CurrentRound,
        RoundResultsJson = k.RoundResultsJson,
        Competitor1Id = k.Competitor1Id,
        Competitor2Id = k.Competitor2Id,
        WinnerId = k.WinnerId,
        LooserId = k.LooserId,
        IsFinished = k.IsFinished,
        GlobalOrder = k.GlobalOrder
    };
    #endregion

    #region Pools (Poules)
    private void BuildPoolMatches(List<DrawPoolsModel> pools)
    {
        var items = new ObservableCollection<ServerPoolMatchItem>();
        foreach (var p in pools)
        {
            items.Add(new ServerPoolMatchItem
            {
                Model = p,
                MatchLabel = $"Match #{p.Order}",
                Competitor1 = p.Competitor1,
                Competitor2 = p.Competitor2,
                IsFinished = p.IsFinished,
                CanReset = p.IsFinished || p.CurrentRound > 1 || !string.IsNullOrWhiteSpace(p.RoundResultsJson) || (p.RefereeDatas?.Count > 0),
                IsCompetitor1Winner = p.IsFinished && p.WinnerId.HasValue && p.WinnerId == p.Competitor1Id,
                IsCompetitor2Winner = p.IsFinished && p.WinnerId.HasValue && p.WinnerId == p.Competitor2Id
            });
        }
        PoolMatches = items;
        RecalculatePoolStandings(pools);
    }

    private void RecalculatePoolStandings(List<DrawPoolsModel> pools)
    {
        var competitors = new Dictionary<int, CompetitorModel>();
        foreach (var p in pools)
        {
            if (p.Competitor1 != null) competitors[p.Competitor1.Id] = p.Competitor1;
            if (p.Competitor2 != null) competitors[p.Competitor2.Id] = p.Competitor2;
        }

        var standings = competitors.Values.Select(c =>
        {
            int wins = pools.Count(p => p.IsFinished && p.WinnerId == c.Id);
            int losses = pools.Count(p => p.IsFinished && p.LooserId == c.Id);
            return new ServerPoolStandingItem
            {
                CompetitorName = c.Name,
                Wins = wins,
                Losses = losses
            };
        })
        .OrderByDescending(s => s.Wins)
        .ThenBy(s => s.Losses)
        .ThenBy(s => s.CompetitorName)
        .ToList();

        for (int i = 0; i < standings.Count; i++)
        {
            standings[i].Rank = i + 1;
            standings[i].IsLeader = i == 0 && standings[i].Wins > 0;
        }

        PoolStandings = new ObservableCollection<ServerPoolStandingItem>(standings);
    }

    [RelayCommand]
    public async Task SetPoolWinner1(ServerPoolMatchItem? matchItem) => await SetPoolWinnerAsync(matchItem, 1);

    [RelayCommand]
    public async Task SetPoolWinner2(ServerPoolMatchItem? matchItem) => await SetPoolWinnerAsync(matchItem, 2);

    private async Task SetPoolWinnerAsync(ServerPoolMatchItem? matchItem, int side)
    {
        if (matchItem == null || _currentDrawModel == null) return;
        var p = matchItem.Model;

        var winner = side == 1 ? p.Competitor1 : p.Competitor2;
        var looser = side == 1 ? p.Competitor2 : p.Competitor1;
        if (winner == null) return;

        p.WinnerId = winner.Id;
        p.Winner = winner;
        p.LooserId = looser?.Id;
        p.Looser = looser;
        p.IsFinished = true;

        await _drawPoolsRepository.UpdateAsync(ToDetachedPool(p));
        BuildPoolMatches(_currentDrawModel.DrawPools.OrderBy(x => x.Order).ToList());
    }

    [RelayCommand]
    public async Task ResetPoolMatch(ServerPoolMatchItem? matchItem)
    {
        if (matchItem == null || _currentDrawModel == null) return;
        var p = matchItem.Model;

        p.WinnerId = null;
        p.Winner = null;
        p.LooserId = null;
        p.Looser = null;
        p.IsFinished = false;
        p.CurrentRound = 1;
        p.RoundResultsJson = string.Empty;
        p.RefereeDatas?.Clear();

        await _drawPoolsRepository.UpdateAsync(ToDetachedPool(p));
        await DeleteRefereeDatasForMatchAsync(drawPoolsId: p.Id);
        BuildPoolMatches(_currentDrawModel.DrawPools.OrderBy(x => x.Order).ToList());
    }

    private async Task DeleteRefereeDatasForMatchAsync(int? drawKnockoutId = null, int? drawPoolsId = null)
    {
        var allRefereeDatas = await _refereeDataRepository.GetAllAsync();
        if (allRefereeDatas == null) return;

        var toDelete = allRefereeDatas.Where(r =>
            (drawKnockoutId.HasValue && drawKnockoutId.Value > 0 && r.DrawKnockoutId == drawKnockoutId.Value) ||
            (drawPoolsId.HasValue && drawPoolsId.Value > 0 && r.DrawPoolsId == drawPoolsId.Value))
            .ToList();

        foreach (var item in toDelete)
        {
            await _refereeDataRepository.DeleteAsync(item);
        }
    }

    private static DrawPoolsModel ToDetachedPool(DrawPoolsModel p) => new()
    {
        Id = p.Id,
        DrawId = p.DrawId,
        Order = p.Order,
        CurrentRound = p.CurrentRound,
        RoundResultsJson = p.RoundResultsJson,
        Competitor1Id = p.Competitor1Id,
        Competitor2Id = p.Competitor2Id,
        WinnerId = p.WinnerId,
        LooserId = p.LooserId,
        IsFinished = p.IsFinished,
        GlobalOrder = p.GlobalOrder
    };
    #endregion

    #region Taolu (Order + Recalcul du vainqueur)
    private void BuildOrderItems(List<DrawOrderModel> orders, Dictionary<int, List<RefereeDataModel>>? refDatasByOrder = null)
    {
        var items = new List<ServerOrderItem>();
        foreach (var o in orders)
        {
            double? score = null;
            if (!string.IsNullOrWhiteSpace(o.RoundResultsJson) &&
                double.TryParse(o.RoundResultsJson.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                score = Math.Round(parsed, 2);
            }

            var orderItem = new ServerOrderItem
            {
                Model = o,
                Order = o.Order,
                CompetitorName = o.Competitor?.Name ?? $"Compétiteur #{o.CompetitorId}",
                Club = o.Competitor?.Club ?? string.Empty,
                NumericScore = score,
                ScoreText = score.HasValue ? score.Value.ToString("F2", CultureInfo.InvariantCulture) : string.Empty,
                IsFinished = o.IsFinished
            };

            var orderRefDatas = (o.RefereeDatas != null && o.RefereeDatas.Count > 0)
                ? o.RefereeDatas
                : (refDatasByOrder != null && refDatasByOrder.TryGetValue(o.Id, out var fromRepo)
                    ? fromRepo
                    : Enumerable.Empty<RefereeDataModel>());

            var latestByJudge = orderRefDatas
                .Where(r => !string.IsNullOrWhiteSpace(r.Referee))
                .GroupBy(r => r.Referee, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).First(),
                    StringComparer.OrdinalIgnoreCase);

            // Toujours afficher les 9 juges Taolu (A_1..A_3, B_1..B_3, C_1..C_3)
            foreach (var (refName, group, defaultScore) in GetDefaultTaoluJudges())
            {
                latestByJudge.TryGetValue(refName, out var latest);
                double judgeScore = latest != null ? Math.Round(latest.Score, 2) : defaultScore;
                orderItem.JudgeScores.Add(new ServerTaoluJudgeScoreItem
                {
                    OrderItem = orderItem,
                    LatestRecord = latest,
                    RefereeName = refName,
                    Group = group,
                    NumericScore = judgeScore,
                    ScoreText = judgeScore.ToString("F2", CultureInfo.InvariantCulture)
                });
            }

            items.Add(orderItem);
        }

        RecalculateOrderRankings(items);
        OrderItems = new ObservableCollection<ServerOrderItem>(items);
    }

    private void RecalculateOrderRankings(List<ServerOrderItem> items)
    {
        foreach (var it in items)
        {
            it.Rank = null;
            it.IsWinner = false;
        }

        var ranked = items
            .Where(i => i.NumericScore.HasValue)
            .OrderByDescending(i => i.NumericScore!.Value)
            .ThenBy(i => i.Order)
            .ToList();

        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
            ranked[i].IsWinner = i == 0;
        }

        TaoluWinnerName = ranked.Count > 0
            ? $"{ranked[0].CompetitorName} ({ranked[0].NumericScore!.Value:F2} pts)"
            : "Aucun score validé";
    }

    [RelayCommand]
    public async Task SaveOrderScore(ServerOrderItem? item)
    {
        if (item == null || _currentDrawModel == null) return;

        if (!double.TryParse((item.ScoreText ?? string.Empty).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) || parsed < 0)
        {
            await _alertService.ShowAlert("Note invalide", "Veuillez saisir un score numérique valide (ex: 8.75).", "OK");
            return;
        }

        double rounded = Math.Round(parsed, 2);
        item.NumericScore = rounded;
        item.ScoreText = rounded.ToString("F2", CultureInfo.InvariantCulture);
        item.IsFinished = true;

        item.Model.RoundResultsJson = item.ScoreText;
        item.Model.IsFinished = true;

        await _drawOrderRepository.UpdateAsync(ToDetachedOrder(item.Model));

        var list = OrderItems.ToList();
        RecalculateOrderRankings(list);
        OrderItems = new ObservableCollection<ServerOrderItem>(list);
    }

    [RelayCommand]
    public async Task SaveJudgeScore(ServerTaoluJudgeScoreItem? judgeItem)
    {
        if (judgeItem == null || _currentDrawModel == null) return;

        if (!double.TryParse((judgeItem.ScoreText ?? string.Empty).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) || parsed < 0)
        {
            await _alertService.ShowAlert("Note invalide", "Veuillez saisir une note de juge valide (ex: 4.80).", "OK");
            return;
        }

        await PersistJudgeScoreAsync(judgeItem, parsed);

        var orderItem = judgeItem.OrderItem;
        double totalScore = ComputeTaoluTotalScore(orderItem.JudgeScores);
        orderItem.NumericScore = totalScore;
        orderItem.ScoreText = totalScore.ToString("F2", CultureInfo.InvariantCulture);
        orderItem.IsFinished = true;
        orderItem.Model.RoundResultsJson = orderItem.ScoreText;
        orderItem.Model.IsFinished = true;
        await _drawOrderRepository.UpdateAsync(ToDetachedOrder(orderItem.Model));

        var list = OrderItems.ToList();
        RecalculateOrderRankings(list);
        OrderItems = new ObservableCollection<ServerOrderItem>(list);
    }

    private async Task PersistJudgeScoreAsync(ServerTaoluJudgeScoreItem judgeItem, double parsedScore)
    {
        double rounded = Math.Round(parsedScore, 2);
        judgeItem.NumericScore = rounded;
        judgeItem.ScoreText = rounded.ToString("F2", CultureInfo.InvariantCulture);

        if (judgeItem.LatestRecord != null)
        {
            judgeItem.LatestRecord.Score = rounded;
            judgeItem.LatestRecord.IsCorrection = true;

            await _refereeDataRepository.UpdateAsync(new RefereeDataModel
            {
                Id = judgeItem.LatestRecord.Id,
                Referee = judgeItem.LatestRecord.Referee,
                Date = judgeItem.LatestRecord.Date,
                MatchTimer = judgeItem.LatestRecord.MatchTimer,
                Score = rounded,
                Round = judgeItem.LatestRecord.Round,
                Data = judgeItem.LatestRecord.Data,
                IsCorrection = true,
                CompetitorId = judgeItem.LatestRecord.CompetitorId,
                DrawKnockoutId = judgeItem.LatestRecord.DrawKnockoutId,
                DrawOrderId = judgeItem.LatestRecord.DrawOrderId,
                DrawPoolsId = judgeItem.LatestRecord.DrawPoolsId
            });
        }
        else
        {
            var newRecord = new RefereeDataModel
            {
                Referee = judgeItem.RefereeName,
                Date = DateTime.UtcNow,
                Score = rounded,
                Round = judgeItem.OrderItem.Model.CurrentRound > 0 ? judgeItem.OrderItem.Model.CurrentRound : 1,
                Data = string.Empty,
                IsCorrection = true,
                CompetitorId = judgeItem.OrderItem.Model.CompetitorId,
                DrawOrderId = judgeItem.OrderItem.Model.Id
            };
            await _refereeDataRepository.AddAsync(newRecord);
            judgeItem.LatestRecord = newRecord;
        }
    }

    private static IEnumerable<(string RefereeName, string Group, double DefaultScore)> GetDefaultTaoluJudges()
    {
        for (int i = 1; i <= 3; i++) yield return ($"A_{i}", "A", Constants.DEFAULT_SCORE_GROUP_A);
        for (int i = 1; i <= 3; i++) yield return ($"B_{i}", "B", Constants.DEFAULT_SCORE_GROUP_B);
        for (int i = 1; i <= 3; i++) yield return ($"C_{i}", "C", Constants.DEFAUTL_SCORE_GROUP_C);
    }

    /// <summary>
    /// Calcule la note totale Taolu à partir des notes des juges :
    /// moyenne par groupe (A, B, C) arrondie à 2 décimales, puis somme des groupes.
    /// </summary>
    public static double ComputeTaoluTotalScore(IEnumerable<ServerTaoluJudgeScoreItem> judgeScores)
    {
        double total = judgeScores
            .GroupBy(j => j.Group, StringComparer.OrdinalIgnoreCase)
            .Sum(g => Math.Round(g.Average(j => j.NumericScore), 2));
        return Math.Round(total, 2);
    }

    [RelayCommand]
    public async Task RecalculateAllOrderScores()
    {
        if (_currentDrawModel == null) return;

        var list = OrderItems.ToList();
        foreach (var item in list)
        {
            bool judgeEdited = false;
            foreach (var j in item.JudgeScores)
            {
                if (double.TryParse((j.ScoreText ?? string.Empty).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double jParsed) && jParsed >= 0)
                {
                    double jRounded = Math.Round(jParsed, 2);
                    if (Math.Abs(jRounded - j.NumericScore) > 0.0001)
                    {
                        await PersistJudgeScoreAsync(j, jRounded);
                        judgeEdited = true;
                    }
                }
            }

            if (judgeEdited && item.JudgeScores.Count > 0)
            {
                double totalScore = ComputeTaoluTotalScore(item.JudgeScores);
                item.NumericScore = totalScore;
                item.ScoreText = totalScore.ToString("F2", CultureInfo.InvariantCulture);
                item.IsFinished = true;
                item.Model.RoundResultsJson = item.ScoreText;
                item.Model.IsFinished = true;
                await _drawOrderRepository.UpdateAsync(ToDetachedOrder(item.Model));
            }
            else if (double.TryParse((item.ScoreText ?? string.Empty).Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) && parsed >= 0)
            {
                double rounded = Math.Round(parsed, 2);
                item.NumericScore = rounded;
                item.ScoreText = rounded.ToString("F2", CultureInfo.InvariantCulture);
                item.IsFinished = true;
                item.Model.RoundResultsJson = item.ScoreText;
                item.Model.IsFinished = true;
                await _drawOrderRepository.UpdateAsync(ToDetachedOrder(item.Model));
            }
        }

        RecalculateOrderRankings(list);
        OrderItems = new ObservableCollection<ServerOrderItem>(list);
    }

    private static DrawOrderModel ToDetachedOrder(DrawOrderModel o) => new()
    {
        Id = o.Id,
        DrawId = o.DrawId,
        Order = o.Order,
        CurrentRound = o.CurrentRound,
        RoundResultsJson = o.RoundResultsJson,
        CompetitorId = o.CompetitorId,
        IsFinished = o.IsFinished,
        GlobalOrder = o.GlobalOrder
    };
    #endregion
}

#region Item ViewModels
public class ServerKnockoutRoundItem
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<ServerKnockoutMatchItem> Matches { get; set; } = new();
}

public class ServerKnockoutMatchItem
{
    public DrawKnockoutModel Model { get; set; } = null!;
    public string StageLabel { get; set; } = string.Empty;
    public bool IsFinished { get; set; }
    public bool CanReset { get; set; }
    public bool IsBye { get; set; }
    public string WinnerName { get; set; } = string.Empty;
    public double BracketSlotHeight { get; set; } = 180;
    public double HalfSlotHeight => BracketSlotHeight / 2.0;
    public bool ShowLeftConnector { get; set; }
    public bool ShowRightConnector { get; set; }
    public bool IsTopFeeder { get; set; }
    public bool IsTopFeederConnector => ShowRightConnector && IsTopFeeder;
    public bool IsBottomFeederConnector => ShowRightConnector && !IsTopFeeder;
    public ServerKnockoutSlotItem Slot1 { get; set; } = null!;
    public ServerKnockoutSlotItem Slot2 { get; set; } = null!;
}

public class ServerKnockoutSlotItem
{
    public ServerKnockoutMatchItem MatchItem { get; set; } = null!;
    public int SlotNumber { get; set; }
    public CompetitorModel? Competitor { get; set; }
    public string CompetitorName => Competitor?.Name ?? "(En attente / BYE)";
    public string Club => Competitor?.Club ?? string.Empty;
    public bool HasCompetitor => Competitor != null;
    public bool IsWinner { get; set; }
    public bool IsLoser { get; set; }
}

public class ServerPoolMatchItem
{
    public DrawPoolsModel Model { get; set; } = null!;
    public string MatchLabel { get; set; } = string.Empty;
    public CompetitorModel? Competitor1 { get; set; }
    public CompetitorModel? Competitor2 { get; set; }
    public string Competitor1Name => Competitor1?.Name ?? "?";
    public string Competitor2Name => Competitor2?.Name ?? "?";
    public bool IsFinished { get; set; }
    public bool CanReset { get; set; }
    public bool IsCompetitor1Winner { get; set; }
    public bool IsCompetitor2Winner { get; set; }
}

public class ServerPoolStandingItem
{
    public int Rank { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public bool IsLeader { get; set; }
    public string RankDisplay => IsLeader ? $"🏆 #{Rank}" : $"#{Rank}";
}

public partial class ServerOrderItem : ObservableObject
{
    public DrawOrderModel Model { get; set; } = null!;
    public int Order { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;
    public double? NumericScore { get; set; }

    [ObservableProperty]
    private string _scoreText = string.Empty;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    private int? _rank;

    [ObservableProperty]
    private bool _isWinner;

    public string RankDisplay => Rank.HasValue ? (IsWinner ? $"🏆 #{Rank}" : $"#{Rank}") : "—";

    public ObservableCollection<ServerTaoluJudgeScoreItem> JudgeScores { get; set; } = new();

    public bool HasJudgeScores => JudgeScores.Count > 0;
}

public partial class ServerTaoluJudgeScoreItem : ObservableObject
{
    public ServerOrderItem OrderItem { get; set; } = null!;
    public RefereeDataModel? LatestRecord { get; set; }
    public string RefereeName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public double NumericScore { get; set; }

    [ObservableProperty]
    private string _scoreText = string.Empty;
}
#endregion
