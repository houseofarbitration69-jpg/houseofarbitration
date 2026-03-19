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
                    Competitors = new ObservableCollection<CompetitorModel>(value.Competitors ?? new());
                    if (IsElimination)
                    {
                        InitializeBracket();
                    }
                }
                OnPropertyChanged(nameof(IsElimination));
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

        // Prepare Round 1 matches
        var round1 = new BracketRoundViewModel { Name = "Round 1" };
        var matchesInRound = m / 2;
        
        for (int i = 0; i < matchesInRound; i++)
        {
            var match = new BracketMatchViewModel();
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
                nextRound.Matches.Add(new BracketMatchViewModel());
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

        // Update Category.Competitors list if we are in Round 1
        // (This is a bit simplified, but ensures persistence for basic draws)
        UpdateCategoryCompetitors();

        DraggedSlot = null;
        
        // Notify changes to ensure UI refresh on all platforms
        OnPropertyChanged(nameof(Rounds));
    }

    private void UpdateCategoryCompetitors()
    {
        if (Category == null || Rounds.Count == 0) return;

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

    public BracketSlotViewModel Slot1 { get; } = new();
    public BracketSlotViewModel Slot2 { get; } = new();
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
