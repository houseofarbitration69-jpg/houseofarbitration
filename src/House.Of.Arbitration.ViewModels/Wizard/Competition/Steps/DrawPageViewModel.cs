#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class DrawPageViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IRepository<DrawModel> _repository;
    #endregion

    #region Attributs
    private CategoryModel? _category;
    private ObservableCollection<CompetitorModel?> _competitors = new();
    private ObservableCollection<BracketRoundViewModel> _rounds = new();
    private List<BracketSlotViewModel> _pouleSlots = new();
    private BracketSlotViewModel? _draggedSlot;
    private bool _isDragging;
    #endregion

    #region Properties    
    public BracketSlotViewModel? DraggedSlot
    {
        get => _draggedSlot;
        set => SetProperty(ref _draggedSlot, value);
    }

    public bool IsDragging
    {
        get => _isDragging;
        set => SetProperty(ref _isDragging, value);
    }

    public CategoryModel? Category
    {
        get => _category;
        set
        {
            if (SetProperty(ref _category, value))
            {
                if (value != null)
                {
                    if (value.Type == CategoryType.Taolu)
                    {
                        value.RoundType = RoundType.Order;
                    }

                    var competitorModels = value.Competitors?.Select(cc => cc.Competitor).ToList() ?? new();

                    if (competitorModels != null)
                    {
                        Competitors = new ObservableCollection<CompetitorModel?>(competitorModels);
                    }
                }

                OnPropertyChanged(nameof(IsKnockouts));
                OnPropertyChanged(nameof(IsPools));
                OnPropertyChanged(nameof(IsOrder));
            }
        }
    }

    public ObservableCollection<CompetitorModel?> Competitors
    {
        get => _competitors;
        set => SetProperty(ref _competitors, value);
    }

    public ObservableCollection<BracketRoundViewModel> Rounds
    {
        get => _rounds;
        set => SetProperty(ref _rounds, value);
    }

    public bool IsKnockouts => Category?.RoundType == RoundType.Knockouts;
    public bool IsPools => Category?.RoundType == RoundType.Pools;
    public bool IsOrder => Category?.RoundType == RoundType.Order;
    #endregion

    #region Constructors
    public DrawPageViewModel(IPopupService popupService, ILogger<DrawPageViewModel> logger, ResourceProvider resourceProvider, IRepository<DrawModel> repository)
        : base(logger, resourceProvider, popupService)
    {
        _repository = repository;
    }
    #endregion

    #region Implement IQueryAttributable
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(nameof(Category)))
        {
            Category = (CategoryModel?)query[nameof(Category)];
            await LoadDrawAsync();
        }
    }
    #endregion

    #region Private Methods
    private async Task LoadDrawAsync()
    {
        if (Category == null) return;

        IsBusy = true;
        try
        {
            // Try to load existing draw from DB

            if (IsKnockouts)
            {
                var draws = await _repository.GetAllAsync("DrawKnockouts.Competitor1", "DrawKnockouts.Competitor2");
                var existingDraw = draws?.FirstOrDefault(d => d.CategoryId == Category.Id);

                InitializeKnockout(existingDraw);
            }
            else if (IsPools)
            {
                var draws = await _repository.GetAllAsync("DrawPools.Competitor1", "DrawPools.Competitor2");
                var existingDraw = draws?.FirstOrDefault(d => d.CategoryId == Category.Id);

                InitializePools(existingDraw);
            }
            else if (IsOrder)
            {
                var draws = await _repository.GetAllAsync("DrawOrders.Competitor");
                var existingDraw = draws?.FirstOrDefault(d => d.CategoryId == Category.Id);

                InitializeOrder(existingDraw);
            }

            OnPropertyChanged(nameof(Rounds));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InitializeKnockout(DrawModel? existingDraw = null)
    {
        if (Category == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();

        int n = Category.Competitors.Count;
        int m = 1;
        while (m < n) m *= 2;
        if (m < 2) m = 2; // Min 1 match

        int globalMatchOrder = 1;

        // Prepare Round 1 matches
        var round1 = new BracketRoundViewModel { Name = "Round 1" };
        var matchesInRound = m / 2;

        for (int i = 0; i < matchesInRound; i++)
        {
            var match = new BracketMatchViewModel();
            match.Order = i + 1;

            if (existingDraw != null)
            {
                var savedMatch = existingDraw.DrawKnockouts?.FirstOrDefault(ms => ms.Order == match.Order);
                if (savedMatch != null)
                {
                    match.Slot1.Competitor = savedMatch.Competitor1;
                    match.Slot2.Competitor = savedMatch.Competitor2;
                    match.GlobalOrder = savedMatch.GlobalOrder;
                    if (match.GlobalOrder >= globalMatchOrder) globalMatchOrder = match.GlobalOrder + 1;
                }
            }
            else
            {
                match.Slot1.Competitor = (i * 2 < n) ? Category.Competitors[i * 2].Competitor : null;
                match.Slot2.Competitor = (i * 2 + 1 < n) ? Category.Competitors[i * 2 + 1].Competitor : null;
            }
            round1.Matches.Add(match);
        }
        newRounds.Add(round1);

        // Prepare subsequent rounds
        int currentMatches = matchesInRound;
        int roundIndex = 2;
        int matchOffset = matchesInRound;
        while (currentMatches > 1)
        {
            currentMatches /= 2;
            var nextRound = new BracketRoundViewModel { Name = $"Round {roundIndex++}" };
            for (int i = 0; i < currentMatches; i++)
            {
                var match = new BracketMatchViewModel();
                match.Order = matchOffset + i + 1;

                if (existingDraw != null)
                {
                    var savedMatch = existingDraw.DrawKnockouts?.FirstOrDefault(ms => ms.Order == match.Order);
                    if (savedMatch != null)
                    {
                        match.Slot1.Competitor = savedMatch.Competitor1;
                        match.Slot2.Competitor = savedMatch.Competitor2;
                        match.GlobalOrder = savedMatch.GlobalOrder;
                        if (match.GlobalOrder >= globalMatchOrder) globalMatchOrder = match.GlobalOrder + 1;
                    }
                }

                nextRound.Matches.Add(match);
            }
            matchOffset += currentMatches;
            newRounds.Add(nextRound);
        }

        // Add Winner slot round
        var winnerRound = new BracketRoundViewModel { Name = "Winner" };
        winnerRound.Matches.Add(new BracketMatchViewModel { IsWinnerSlot = true });
        newRounds.Add(winnerRound);

        CalculateMargins(newRounds);

        Rounds = newRounds;

        if (existingDraw == null)
        {
            RefreshAdvancements();
        }
        else
        {
            // Even if existing draw, we need to set IsBye flags for UI visibility
            UpdateByeFlags();
        }
    }

    private void UpdateByeFlags()
    {
        if (Rounds.Count == 0) return;
        var round1 = Rounds[0];
        foreach (var match in round1.Matches)
        {
            int count = (match.Slot1.Competitor != null ? 1 : 0) + (match.Slot2.Competitor != null ? 1 : 0);
            match.IsBye = count < 2;
        }
        // Round 2+ are never byes for order selection
        for (int r = 1; r < Rounds.Count; r++)
        {
            foreach (var match in Rounds[r].Matches)
            {
                match.IsBye = false;
            }
        }
    }

    private void RefreshAdvancements()
    {
        if (Rounds.Count < 2 || (!IsKnockouts && !IsPools)) return;

        if (IsKnockouts)
        {
            // Clear all rounds except Round 1 to re-calculate everything
            for (int r = 1; r < Rounds.Count; r++)
            {
                foreach (var match in Rounds[r].Matches)
                {
                    match.Slot1.Competitor = null;
                    match.Slot2.Competitor = null;
                    match.IsBye = false;
                }
            }

            int globalMatchOrder = 1;

            // Propagate byes through all rounds and re-calculate GlobalOrder
            for (int r = 0; r < Rounds.Count - 1; r++)
            {
                var currentRound = Rounds[r];
                var nextRound = Rounds[r + 1];

                for (int i = 0; i < currentRound.Matches.Count; i++)
                {
                    var match = currentRound.Matches[i];
                    if (match.IsWinnerSlot) continue;

                    int competitorsCount = (match.Slot1.Competitor != null ? 1 : 0) + (match.Slot2.Competitor != null ? 1 : 0);
                    CompetitorModel? winner = match.Slot1.Competitor ?? match.Slot2.Competitor;

                    // 1. Only Round 1 matches can be byes for skipping order selection
                    if (r == 0)
                    {
                        match.IsBye = (competitorsCount < 2);
                    }
                    else
                    {
                        match.IsBye = false;
                    }

                    // 2. Assign GlobalOrder
                    // Round 1: match gets an order if there is at least one competitor (bye or real match)
                    // so it appears in the competition flow.
                    // Round 2+: always gets an order.
                    if (r == 0)
                    {
                        if (competitorsCount > 0)
                            match.GlobalOrder = globalMatchOrder++;
                        else
                            match.GlobalOrder = 0;
                    }
                    else
                    {
                        match.GlobalOrder = globalMatchOrder++;
                    }

                    // 3. Propagation logic
                    // If exactly one competitor in this match, they advance to next round
                    if (competitorsCount == 1)
                    {
                        if (nextRound.Matches.Count > 0)
                        {
                            var nextMatch = nextRound.Matches[0]; // Default for Winner slot
                            
                            if (!nextMatch.IsWinnerSlot)
                            {
                                int nextMatchIndex = i / 2;
                                if (nextMatchIndex < nextRound.Matches.Count)
                                {
                                    nextMatch = nextRound.Matches[nextMatchIndex];
                                    if (i % 2 == 0)
                                        nextMatch.Slot1.Competitor = winner;
                                    else
                                        nextMatch.Slot2.Competitor = winner;
                                }
                            }
                            else
                            {
                                // It's the winner slot. 
                                // User: "on ne peux définir le gagnant que lorsque l'ensemble des matches a été fait"
                                // We only show the winner name if there is ONLY 1 competitor in the whole category.
                                if (Category?.Competitors.Count == 1)
                                {
                                    nextMatch.Slot1.Competitor = winner;
                                }
                                else
                                {
                                    nextMatch.Slot1.Competitor = null; // Stays "WINNER"
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (IsPools)
        {
            // Points-based system, no automatic winner advancement displayed in this view
        }
    }

    private void InitializePools(DrawModel? existingDraw = null)
    {
        if (Category == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();
        var pouleRound = new BracketRoundViewModel { Name = "Matchs de Poule" };

        var competitors = Category.Competitors;
        int n = competitors.Count;
        int matchOrder = 1;
        int globalMatchOrder = 1;

        // Create shared slots for each competitor position in the poule
        _pouleSlots = new List<BracketSlotViewModel>();
        for (int i = 0; i < n; i++)
        {
            _pouleSlots.Add(new BracketSlotViewModel { Competitor = competitors[i].Competitor });
        }

        // Generate all unique combinations (everyone against everyone)
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var match = new BracketMatchViewModel();
                match.Order = matchOrder; // Will be assigned GlobalOrder

                if (existingDraw != null)
                {
                    var savedMatch = existingDraw.DrawPools?.FirstOrDefault(ms => ms.Order == match.Order);
                    if (savedMatch != null)
                    {
                        match.Slot1 = new BracketSlotViewModel { Competitor = savedMatch.Competitor1 };
                        match.Slot2 = new BracketSlotViewModel { Competitor = savedMatch.Competitor2 };
                        match.Order = savedMatch.Order;
                        match.GlobalOrder = savedMatch.GlobalOrder;
                        if (match.Order >= matchOrder) matchOrder = match.Order + 1;
                        if (match.GlobalOrder >= globalMatchOrder) globalMatchOrder = match.GlobalOrder + 1;
                    }
                    else
                    {
                        match.Slot1 = _pouleSlots[i];
                        match.Slot2 = _pouleSlots[j];
                        match.Order = matchOrder++;
                        match.GlobalOrder = globalMatchOrder++;
                    }
                }
                else
                {
                    match.Slot1 = _pouleSlots[i];
                    match.Slot2 = _pouleSlots[j];
                    match.Order = matchOrder++;
                    match.GlobalOrder = globalMatchOrder++;
                }

                match.Height = 140;
                match.Margin = new Thickness(0, 10, 0, 10);
                pouleRound.Matches.Add(match);
            }
        }

        newRounds.Add(pouleRound);

        Rounds = newRounds;

        if (existingDraw == null)
        {
            RefreshAdvancements();
        }
    }

    private void InitializeOrder(DrawModel? existingDraw = null)
    {
        if (Category == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();
        var orderRound = new BracketRoundViewModel { Name = "Ordre de passage" };

        var competitors = Category.Competitors;
        int n = competitors.Count;
        int globalMatchOrder = 1;

        _pouleSlots = new List<BracketSlotViewModel>();

        if (existingDraw != null && existingDraw.DrawOrders != null)
        {
            var sortedMatches = existingDraw.DrawOrders.OrderBy(ms => ms.Order).ToList();
            foreach (var savedMatch in sortedMatches)
            {
                var slot = new BracketSlotViewModel { Competitor = savedMatch.Competitor };
                _pouleSlots.Add(slot);

                var match = new BracketMatchViewModel();
                match.Order = savedMatch.Order;
                match.GlobalOrder = savedMatch.GlobalOrder;
                match.Slot1 = slot;
                match.Height = 100;
                match.Margin = new Thickness(0, 5, 0, 5);
                orderRound.Matches.Add(match);
                if (match.GlobalOrder >= globalMatchOrder) globalMatchOrder = match.GlobalOrder + 1;
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                var slot = new BracketSlotViewModel { Competitor = competitors[i].Competitor };
                _pouleSlots.Add(slot);

                var match = new BracketMatchViewModel();
                match.Order = i + 1;
                match.GlobalOrder = globalMatchOrder++;
                match.Slot1 = slot;
                match.Height = 100;
                match.Margin = new Thickness(0, 5, 0, 5);
                orderRound.Matches.Add(match);
            }
        }

        newRounds.Add(orderRound);
        Rounds = newRounds;
    }

    private void CalculateMargins(ObservableCollection<BracketRoundViewModel> targetRounds)
    {
        double matchHeight = 140;
        double currentMargin = 0;
        double currentSpacing = 0;
        double previousRoundMargin = 0;

        for (int i = 0; i < targetRounds.Count; i++)
        {
            var round = targetRounds[i];

            if (round.Name == "Winner")
            {
                foreach (var match in round.Matches)
                {
                    match.Height = matchHeight;
                    // Align perfectly with the first match of the previous round
                    match.Margin = new Thickness(0, previousRoundMargin, 0, 0);
                }
                break;
            }

            foreach (var match in round.Matches)
            {
                match.Height = matchHeight;
                if (match == round.Matches[0])
                {
                    match.Margin = new Thickness(0, currentMargin, 0, 0);
                }
                else
                {
                    match.Margin = new Thickness(0, currentSpacing, 0, 0);
                }
            }

            previousRoundMargin = currentMargin;
            currentMargin = (currentMargin * 2) + (matchHeight / 2);
            currentSpacing = (currentSpacing * 2) + matchHeight;
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Save()
    {
        if (Category == null) return;

        try
        {
            // Clear tracker at the beginning to ensure we start from a clean state
            _repository.ClearTracker();

            // Delete existing draws for this category
            var existingDraws = await _repository.GetAllAsync();
            if (existingDraws != null)
            {
                // Materialize the list with ToList() to avoid modification issues and ensure it's not a dynamic query
                foreach (var existingDraw in existingDraws.Where(d => d.CategoryId == Category.Id).ToList())
                {
                    // Null out navigation properties to avoid EF tracking conflicts during delete
                    existingDraw.Category = null;
                    existingDraw.DrawKnockouts = null;
                    existingDraw.DrawPools = null;
                    existingDraw.DrawOrders = null;
                    await _repository.DeleteAsync(existingDraw);
                }
            }

            // Update Category.Competitors as before
            UpdateCategoryCompetitors();

            // Prepare DrawModel to save
            var draw = new DrawModel
            {
                CategoryId = Category.Id,
                DrawKnockouts = new List<DrawKnockoutModel>(),
                DrawPools = new List<DrawPoolsModel>(),
                DrawOrders = new List<DrawOrderModel>()
            };

            // Gather all matches from all rounds
            foreach (var round in Rounds)
            {
                foreach (var match in round.Matches)
                {
                    if (match.IsWinnerSlot || match.GlobalOrder == 0) continue;

                    if (Category.RoundType == RoundType.Knockouts)
                    {
                        var knockoutMatch = new DrawKnockoutModel
                        {
                            Draw = draw,
                            Order = match.Order,
                            GlobalOrder = match.GlobalOrder,
                            Competitor1Id = match.Slot1.Competitor?.Id,
                            Competitor2Id = match.Slot2.Competitor?.Id,
                        };

                        // If it's a Round 1 bye (only one competitor), set winner and mark as finished
                        bool isRound1 = round == Rounds.FirstOrDefault();
                        int compCount = (match.Slot1.Competitor != null ? 1 : 0) + (match.Slot2.Competitor != null ? 1 : 0);

                        if (isRound1 && compCount == 1)
                        {
                            knockoutMatch.WinnerId = match.Slot1.Competitor?.Id ?? match.Slot2.Competitor?.Id;
                            knockoutMatch.IsFinished = true;
                        }

                        draw.DrawKnockouts.Add(knockoutMatch);
                    }
                    else if (Category.RoundType == RoundType.Pools)
                    {
                        draw.DrawPools.Add(new DrawPoolsModel
                        {
                            Draw = draw,
                            Order = match.Order,
                            GlobalOrder = match.GlobalOrder,
                            Competitor1Id = match.Slot1.Competitor?.Id,
                            Competitor2Id = match.Slot2.Competitor?.Id,
                        });
                    }
                    else if (Category.RoundType == RoundType.Order)
                    {
                        draw.DrawOrders.Add(new DrawOrderModel
                        {
                            Draw = draw,
                            Order = match.Order,
                            GlobalOrder = match.GlobalOrder,
                            CompetitorId = match.Slot1.Competitor?.Id,
                        });
                    }
                }
            }

            // Persistence
            // Clear tracker again before AddAsync to be absolutely safe
            _repository.ClearTracker();
            await _repository.AddAsync(draw);

            // Notify and go back
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Snackbar.Make("Le tirage et l'ordre des matchs ont été sauvegardés.").Show();
            });

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la sauvegarde du tirage");
            await Shell.Current.DisplayAlertAsync("Erreur", "Impossible de sauvegarder le tirage.", "OK");
        }
    }

    [RelayCommand]
    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void DragStarting(BracketSlotViewModel slot)
    {
        DraggedSlot = slot;
        IsDragging = true;
    }

    [RelayCommand]
    private void DragOver(DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    [RelayCommand]
    private void Drop(BracketSlotViewModel targetSlot)
    {
        IsDragging = false;
        if (DraggedSlot == null || targetSlot == null || DraggedSlot == targetSlot)
        {
            DraggedSlot = null;
            return;
        }

        // Swap competitors
        var temp = targetSlot.Competitor;
        targetSlot.Competitor = DraggedSlot.Competitor;
        DraggedSlot.Competitor = temp;

        // Update Category.Competitors list
        UpdateCategoryCompetitors();

        if (IsKnockouts || IsPools)
        {
            RefreshAdvancements();
        }

        DraggedSlot = null;

        // Notify changes to ensure UI refresh on all platforms
        OnPropertyChanged(nameof(Rounds));
    }

    private void UpdateCategoryCompetitors()
    {
        if (Category == null) return;

        var newLinks = new List<CompetitorCategoryModel>();

        if (IsKnockouts && Rounds.Count > 0)
        {
            var round1 = Rounds[0];
            
            foreach (var match in round1.Matches)
            {
                if (match.Slot1.Competitor != null)
                {
                    newLinks.Add(new CompetitorCategoryModel 
                    { 
                        CompetitorId = match.Slot1.Competitor.Id, 
                        Competitor = match.Slot1.Competitor,
                        CategoryId = Category.Id, 
                        Category = Category 
                    });
                }
                if (match.Slot2.Competitor != null)
                {
                    newLinks.Add(new CompetitorCategoryModel 
                    { 
                        CompetitorId = match.Slot2.Competitor.Id, 
                        Competitor = match.Slot2.Competitor,
                        CategoryId = Category.Id, 
                        Category = Category 
                    });
                }
            }
        }
        else if (IsPools || IsOrder)
        {
            foreach (var slot in _pouleSlots)
            {
                if (slot.Competitor != null)
                {
                    newLinks.Add(new CompetitorCategoryModel 
                    { 
                        CompetitorId = slot.Competitor.Id, 
                        Competitor = slot.Competitor,
                        CategoryId = Category.Id, 
                        Category = Category 
                    });
                }
            }
        }

        Category.Competitors = newLinks;
    }
    #endregion
}

public partial class BracketRoundViewModel : ObservableObject
{
    #region Attributs
    private string _name = string.Empty;
    #endregion

    #region Properties    
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<BracketMatchViewModel> Matches { get; } = new();
    #endregion
}

public partial class BracketMatchViewModel : ObservableObject
{
    #region Attributs
    private Thickness _margin = new Thickness(0);
    private double _height = 0.0;
    private bool _isWinnerSlot = false;
    private bool _isBye = false;
    private int _order;
    private int _globalOrder;
    private BracketSlotViewModel _slot1 = new();
    private BracketSlotViewModel _slot2 = new();
    #endregion

    #region Properties
    public Thickness Margin
    {
        get => _margin;
        set => SetProperty(ref _margin, value);
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public bool IsWinnerSlot
    {
        get => _isWinnerSlot;
        set => SetProperty(ref _isWinnerSlot, value);
    }

    public bool IsBye
    {
        get => _isBye;
        set
        {
            if (SetProperty(ref _isBye, value))
            {
                OnPropertyChanged(nameof(IsOrderVisible));
            }
        }
    }

    public bool IsOrderVisible => !IsWinnerSlot && !IsBye;

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    public int GlobalOrder
    {
        get => _globalOrder;
        set => SetProperty(ref _globalOrder, value);
    }

    public BracketSlotViewModel Slot1
    {
        get => _slot1;
        set => SetProperty(ref _slot1, value);
    }

    public BracketSlotViewModel Slot2
    {
        get => _slot2;
        set => SetProperty(ref _slot2, value);
    }
    #endregion

    #region Commands
    [RelayCommand]
    private void IncrementOrder()
    {
        //Order++;
        GlobalOrder++;
    }

    [RelayCommand]
    private void DecrementOrder()
    {
        //if (Order > 1)
        //    Order--;

        if (GlobalOrder > 1)
            GlobalOrder--;
    }
    #endregion
}

public partial class BracketSlotViewModel : ObservableObject
{
    #region Attributs
    private CompetitorModel? _competitor;
    #endregion

    #region Properties
    public CompetitorModel? Competitor
    {
        get => _competitor;
        set => SetProperty(ref _competitor, value);
    }
    #endregion
}
