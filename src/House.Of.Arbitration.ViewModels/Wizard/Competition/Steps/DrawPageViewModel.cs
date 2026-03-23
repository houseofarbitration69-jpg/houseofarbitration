#region Imports
using CommunityToolkit.Maui;
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
    private ObservableCollection<CompetitorModel> _competitors = new();
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

                    Competitors = new ObservableCollection<CompetitorModel>(value.Competitors ?? new());
                    if (IsElimination)
                    {
                        InitializeBracket();
                    }
                    else if (IsRobin)
                    {
                        InitializeRobin();
                    }
                    else if (IsOrder)
                    {
                        InitializeOrder();
                    }
                }
                OnPropertyChanged(nameof(IsElimination));
                OnPropertyChanged(nameof(IsRobin));
                OnPropertyChanged(nameof(IsOrder));
                OnPropertyChanged(nameof(Rounds));
            }
        }
    }

    public ObservableCollection<CompetitorModel> Competitors
    {
        get => _competitors;
        set => SetProperty(ref _competitors, value);
    }

    public ObservableCollection<BracketRoundViewModel> Rounds
    {
        get => _rounds;
        set => SetProperty(ref _rounds, value);
    }

    public bool IsElimination => Category?.RoundType == RoundType.Elimination;
    public bool IsRobin => Category?.RoundType == RoundType.Robin;
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
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(nameof(Category)))
        {
            Category = (CategoryModel?)query[nameof(Category)];
        }
    }
    #endregion

    #region Private Methods
    private void InitializeBracket()
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
            match.Order = globalMatchOrder++;
            match.Slot1.Competitor = (i * 2 < n) ? Category.Competitors[i * 2] : null;
            match.Slot2.Competitor = (i * 2 + 1 < n) ? Category.Competitors[i * 2 + 1] : null;
            round1.Matches.Add(match);
        }
        newRounds.Add(round1);

        // Prepare subsequent rounds
        int currentMatches = matchesInRound;
        int roundIndex = 2;
        while (currentMatches > 1)
        {
            currentMatches /= 2;
            var nextRound = new BracketRoundViewModel { Name = $"Round {roundIndex++}" };
            for (int i = 0; i < currentMatches; i++)
            {
                var match = new BracketMatchViewModel();
                match.Order = globalMatchOrder++;
                nextRound.Matches.Add(match);
            }
            newRounds.Add(nextRound);
        }

        // Add Winner slot round
        var winnerRound = new BracketRoundViewModel { Name = "Winner" };
        winnerRound.Matches.Add(new BracketMatchViewModel { IsWinnerSlot = true });
        newRounds.Add(winnerRound);

        CalculateMargins(newRounds);

        Rounds = newRounds;
    }

    private void InitializeRobin()
    {
        if (Category == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();
        var pouleRound = new BracketRoundViewModel { Name = "Matchs de Poule" };

        var competitors = Category.Competitors;
        int n = competitors.Count;
        int globalMatchOrder = 1;

        // Create shared slots for each competitor position in the poule
        _pouleSlots = new List<BracketSlotViewModel>();
        for (int i = 0; i < n; i++)
        {
            _pouleSlots.Add(new BracketSlotViewModel { Competitor = competitors[i] });
        }

        // Generate all unique combinations (everyone against everyone)
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var match = new BracketMatchViewModel();
                match.Order = globalMatchOrder++;
                // Link shared instances
                match.Slot1 = _pouleSlots[i];
                match.Slot2 = _pouleSlots[j];
                match.Height = 100;
                match.Margin = new Thickness(0, 10, 0, 10);
                pouleRound.Matches.Add(match);
            }
        }

        newRounds.Add(pouleRound);
        Rounds = newRounds;
    }

    private void InitializeOrder()
    {
        if (Category == null) return;

        var newRounds = new ObservableCollection<BracketRoundViewModel>();
        var orderRound = new BracketRoundViewModel { Name = "Ordre de passage" };

        var competitors = Category.Competitors;
        int n = competitors.Count;
        int globalMatchOrder = 1;

        _pouleSlots = new List<BracketSlotViewModel>();
        for (int i = 0; i < n; i++)
        {
            var slot = new BracketSlotViewModel { Competitor = competitors[i] };
            _pouleSlots.Add(slot);

            var match = new BracketMatchViewModel();
            match.Order = globalMatchOrder++;
            match.Slot1 = slot;
            // Slot2 remains empty for Order type
            match.Height = 80;
            match.Margin = new Thickness(0, 5, 0, 5);
            orderRound.Matches.Add(match);
        }

        newRounds.Add(orderRound);
        Rounds = newRounds;
    }

    private void CalculateMargins(ObservableCollection<BracketRoundViewModel> targetRounds)
    {
        double matchHeight = 100;
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
            // Update Category.Competitors as before
            UpdateCategoryCompetitors();

            // Prepare DrawModel to save
            var draw = new DrawModel
            {
                CategoryId = Category.Id,
                Category = Category,
                DrawSandas = new List<DrawSandaModel>()
            };

            // Gather all matches from all rounds
            foreach (var round in Rounds)
            {
                foreach (var match in round.Matches)
                {
                    if (match.IsWinnerSlot) continue;

                    draw.DrawSandas.Add(new DrawSandaModel
                    {
                        Draw = draw,
                        Order = match.Order,
                        Competitor1Id = match.Slot1.Competitor?.Id,
                        Competitor1 = match.Slot1.Competitor,
                        Competitor2Id = match.Slot2.Competitor?.Id,
                        Competitor2 = match.Slot2.Competitor
                    });
                }
            }

            // Persistence
            // We might need to check if a draw already exists for this category to use Update instead of Add
            // For now, let's assume Add/Update logic handled by repository or simpler:
            await _repository.AddAsync(draw);

            await Shell.Current.DisplayAlert("Succès", "Le tirage et l'ordre des matchs ont été sauvegardés.", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la sauvegarde du tirage");
            await Shell.Current.DisplayAlert("Erreur", "Impossible de sauvegarder le tirage.", "OK");
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

        DraggedSlot = null;
        
        // Notify changes to ensure UI refresh on all platforms
        OnPropertyChanged(nameof(Rounds));
    }

    private void UpdateCategoryCompetitors()
    {
        if (Category == null) return;

        if (IsElimination && Rounds.Count > 0)
        {
            var round1 = Rounds[0];
            var newCompetitors = new List<CompetitorModel>();
            
            foreach (var match in round1.Matches)
            {
                if (match.Slot1.Competitor != null)
                    newCompetitors.Add(match.Slot1.Competitor);
                if (match.Slot2.Competitor != null)
                    newCompetitors.Add(match.Slot2.Competitor);
            }
            
            Category.Competitors = newCompetitors;
        }
        else if (IsRobin || IsOrder)
        {
            Category.Competitors = _pouleSlots.Select(s => s.Competitor).ToList()!;
        }
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
    private bool _isWinnerSlot =false;
    private int _order;
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

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
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
        Order++;
    }

    [RelayCommand]
    private void DecrementOrder()
    {
        if (Order > 1)
            Order--;
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
