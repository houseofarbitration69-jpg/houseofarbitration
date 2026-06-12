#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

[QueryProperty(nameof(CompetitionId), "CompetitionId")]
public partial class DrawsManagementViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<CompetitionModel> _competitionRepository;
    private readonly IRepository<DrawModel> _drawRepository;
    #endregion

    #region Attributs
    private int _competitionId;
    private CompetitionModel? _competition;
    private ObservableCollection<CategoryModel> _categories = new();
    private CategoryModel? _selectedCategory;
    
    private ObservableCollection<CompetitorModel?> _competitors = new();
    private ObservableCollection<BracketRoundViewModel> _rounds = new();
    private List<BracketSlotViewModel> _pouleSlots = new();
    private BracketSlotViewModel? _draggedSlot;
    private bool _isDragging;
    #endregion

    #region Properties
    public int CompetitionId
    {
        get => _competitionId;
        set
        {
            if (SetProperty(ref _competitionId, value))
            {
                MainThread.BeginInvokeOnMainThread(async () => await LoadCompetitionAsync());
            }
        }
    }

    public CompetitionModel? Competition
    {
        get => _competition;
        set => SetProperty(ref _competition, value);
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
                OnCategoryChanged();
            }
        }
    }

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

    public bool IsKnockouts => SelectedCategory?.RoundType == RoundType.Knockouts;
    public bool IsPools => SelectedCategory?.RoundType == RoundType.Pools;
    public bool IsOrder => SelectedCategory?.RoundType == RoundType.Order;
    #endregion

    #region Constructors
    public DrawsManagementViewModel(
        IPopupService popupService, 
        ILogger<DrawsManagementViewModel> logger, 
        ResourceProvider resourceProvider, 
        IRepository<CompetitionModel> competitionRepository,
        IRepository<DrawModel> drawRepository)
        : base(logger, resourceProvider, popupService)
    {
        _competitionRepository = competitionRepository;
        _drawRepository = drawRepository;
    }
    #endregion

    #region Private Methods
    private async Task LoadCompetitionAsync()
    {
        IsBusy = true;
        try
        {
            Competition = await _competitionRepository.GetByIdAsync(CompetitionId, c => c.Categories);
            if (Competition != null && Competition.Categories != null)
            {
                Categories = new ObservableCollection<CategoryModel>(Competition.Categories);
                if (Categories.Count > 0)
                {
                    SelectedCategory = Categories[0];
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading competition {Id}", CompetitionId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnCategoryChanged()
    {
        if (SelectedCategory == null)
        {
            Rounds.Clear();
            Competitors.Clear();
            return;
        }

        IsBusy = true;
        
        // Give UI thread a chance to show the loading indicator
        await Task.Yield();

        try
        {
            if (SelectedCategory.Type == CategoryType.Taolu)
            {
                SelectedCategory.RoundType = RoundType.Order;
            }

            var categoryRepo = Application.Current?.Handler?.MauiContext?.Services.GetService<IRepository<CategoryModel>>();
            if (categoryRepo != null)
            {
                // Optimization: Split the massive include query into smaller, targeted queries
                // to avoid Cartesian Product performance hit.
                
                // 1. Basic category and competitors
                var fullCategory = await categoryRepo.GetByIdAsync(SelectedCategory.Id, "Competitors.Competitor");
                
                if (fullCategory != null)
                {
                    // 2. Fetch Draw separately if it exists
                    var drawRepo = Application.Current?.Handler?.MauiContext?.Services.GetService<IRepository<DrawModel>>();
                    if (drawRepo != null)
                    {
                        var draw = (await drawRepo.GetAllAsync("DrawKnockouts.Competitor1", "DrawKnockouts.Competitor2", "DrawPools.Competitor1", "DrawPools.Competitor2", "DrawOrders.Competitor"))
                                   ?.FirstOrDefault(d => d.CategoryId == SelectedCategory.Id);
                        
                        fullCategory.Draw = draw;
                    }
                    
                    _selectedCategory = fullCategory;
                }
            }

            var competitorModels = SelectedCategory.Competitors?.Select(cc => cc.Competitor).ToList() ?? new();
            Competitors = new ObservableCollection<CompetitorModel?>(competitorModels);

            OnPropertyChanged(nameof(IsKnockouts));
            OnPropertyChanged(nameof(IsPools));
            OnPropertyChanged(nameof(IsOrder));

            // Delay draw loading slightly to ensure UI is ready
            await Task.Delay(50);
            await LoadDrawAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing category");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDrawAsync()
    {
        if (SelectedCategory == null) return;

        // Ensure we are off the main thread for heavy calculations if needed
        // but MAUI UI updates (setting Rounds) must eventually hit main thread.
        
        await Task.Run(() => 
        {
            var existingDraw = SelectedCategory.Draw;

            if (IsKnockouts)
            {
                InitializeKnockout(existingDraw);
            }
            else if (IsPools)
            {
                InitializePools(existingDraw);
            }
            else if (IsOrder)
            {
                InitializeOrder(existingDraw);
            }
        });

        OnPropertyChanged(nameof(Rounds));
    }

    private void InitializeKnockout(DrawModel? existingDraw = null)
    {
        if (SelectedCategory == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();

        int n = SelectedCategory.Competitors.Count;
        int m = 1;
        while (m < n) m *= 2;
        if (m < 2) m = 2;

        int globalMatchOrder = 1;
        var round1 = new BracketRoundViewModel { Name = "Round 1" };
        var matchesInRound = m / 2;

        for (int i = 0; i < matchesInRound; i++)
        {
            var match = new BracketMatchViewModel();
            match.Order = i + 1;

            if (existingDraw != null && existingDraw.DrawKnockouts != null)
            {
                var savedMatch = existingDraw.DrawKnockouts.FirstOrDefault(ms => ms.Order == match.Order);
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
                match.Slot1.Competitor = (i * 2 < n) ? SelectedCategory.Competitors[i * 2].Competitor : null;
                match.Slot2.Competitor = (i * 2 + 1 < n) ? SelectedCategory.Competitors[i * 2 + 1].Competitor : null;
            }
            round1.Matches.Add(match);
        }
        newRounds.Add(round1);

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

                if (existingDraw != null && existingDraw.DrawKnockouts != null)
                {
                    var savedMatch = existingDraw.DrawKnockouts.FirstOrDefault(ms => ms.Order == match.Order);
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

                    if (r == 0)
                    {
                        match.IsBye = (competitorsCount < 2);
                    }
                    else
                    {
                        match.IsBye = false;
                    }

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

                    if (competitorsCount == 1)
                    {
                        if (nextRound.Matches.Count > 0)
                        {
                            var nextMatch = nextRound.Matches[0];
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
                                if (SelectedCategory?.Competitors.Count == 1)
                                {
                                    nextMatch.Slot1.Competitor = winner;
                                }
                                else
                                {
                                    nextMatch.Slot1.Competitor = null;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void InitializePools(DrawModel? existingDraw = null)
    {
        if (SelectedCategory == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();
        var pouleRound = new BracketRoundViewModel { Name = "Matchs de Poule" };

        var competitors = SelectedCategory.Competitors;
        int n = competitors.Count;
        int matchOrder = 1;
        int globalMatchOrder = 1;

        _pouleSlots = new List<BracketSlotViewModel>();
        for (int i = 0; i < n; i++)
        {
            _pouleSlots.Add(new BracketSlotViewModel { Competitor = competitors[i].Competitor });
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var match = new BracketMatchViewModel();
                match.Order = matchOrder;

                if (existingDraw != null && existingDraw.DrawPools != null)
                {
                    var savedMatch = existingDraw.DrawPools.FirstOrDefault(ms => ms.Order == match.Order);
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
    }

    private void InitializeOrder(DrawModel? existingDraw = null)
    {
        if (SelectedCategory == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();
        var orderRound = new BracketRoundViewModel { Name = "Ordre de passage" };

        var competitors = SelectedCategory.Competitors;
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
                    match.Margin = new Thickness(0, previousRoundMargin, 0, 0);
                }
                break;
            }

            foreach (var match in round.Matches)
            {
                match.Height = matchHeight;
                if (match == round.Matches[0])
                    match.Margin = new Thickness(0, currentMargin, 0, 0);
                else
                    match.Margin = new Thickness(0, currentSpacing, 0, 0);
            }

            previousRoundMargin = currentMargin;
            currentMargin = (currentMargin * 2) + (matchHeight / 2);
            currentSpacing = (currentSpacing * 2) + matchHeight;
        }
    }

    private void UpdateCategoryCompetitors()
    {
        if (SelectedCategory == null) return;

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
                        CategoryId = SelectedCategory.Id, 
                        Category = SelectedCategory 
                    });
                }
                if (match.Slot2.Competitor != null)
                {
                    newLinks.Add(new CompetitorCategoryModel 
                    { 
                        CompetitorId = match.Slot2.Competitor.Id, 
                        Competitor = match.Slot2.Competitor,
                        CategoryId = SelectedCategory.Id, 
                        Category = SelectedCategory 
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
                        CategoryId = SelectedCategory.Id, 
                        Category = SelectedCategory 
                    });
                }
            }
        }

        SelectedCategory.Competitors = newLinks;
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Save()
    {
        if (SelectedCategory == null) return;

        try
        {
            _drawRepository.ClearTracker();

            var existingDraws = await _drawRepository.GetAllAsync();
            if (existingDraws != null)
            {
                foreach (var existingDraw in existingDraws.Where(d => d.CategoryId == SelectedCategory.Id).ToList())
                {
                    existingDraw.Category = null;
                    existingDraw.DrawKnockouts = null;
                    existingDraw.DrawPools = null;
                    existingDraw.DrawOrders = null;
                    await _drawRepository.DeleteAsync(existingDraw);
                }
            }

            UpdateCategoryCompetitors();

            var draw = new DrawModel
            {
                CategoryId = SelectedCategory.Id,
                DrawKnockouts = new List<DrawKnockoutModel>(),
                DrawPools = new List<DrawPoolsModel>(),
                DrawOrders = new List<DrawOrderModel>()
            };

            foreach (var round in Rounds)
            {
                foreach (var match in round.Matches)
                {
                    if (match.IsWinnerSlot || match.GlobalOrder == 0) continue;

                    if (SelectedCategory.RoundType == RoundType.Knockouts)
                    {
                        var knockoutMatch = new DrawKnockoutModel
                        {
                            Draw = draw,
                            Order = match.Order,
                            GlobalOrder = match.GlobalOrder,
                            Competitor1Id = match.Slot1.Competitor?.Id,
                            Competitor2Id = match.Slot2.Competitor?.Id,
                        };

                        bool isRound1 = round == Rounds.FirstOrDefault();
                        int compCount = (match.Slot1.Competitor != null ? 1 : 0) + (match.Slot2.Competitor != null ? 1 : 0);

                        if (isRound1 && compCount == 1)
                        {
                            knockoutMatch.WinnerId = match.Slot1.Competitor?.Id ?? match.Slot2.Competitor?.Id;
                            knockoutMatch.IsFinished = true;
                        }

                        draw.DrawKnockouts.Add(knockoutMatch);
                    }
                    else if (SelectedCategory.RoundType == RoundType.Pools)
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
                    else if (SelectedCategory.RoundType == RoundType.Order)
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

            _drawRepository.ClearTracker();
            await _drawRepository.AddAsync(draw);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Snackbar.Make("Le tirage a été sauvegardé.").Show();
            });
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
    private void Drop(BracketSlotViewModel targetSlot)
    {
        IsDragging = false;
        if (DraggedSlot == null || targetSlot == null || DraggedSlot == targetSlot)
        {
            DraggedSlot = null;
            return;
        }

        var temp = targetSlot.Competitor;
        targetSlot.Competitor = DraggedSlot.Competitor;
        DraggedSlot.Competitor = temp;

        UpdateCategoryCompetitors();

        if (IsKnockouts || IsPools)
        {
            RefreshAdvancements();
        }

        DraggedSlot = null;
        OnPropertyChanged(nameof(Rounds));
    }
    #endregion
}
