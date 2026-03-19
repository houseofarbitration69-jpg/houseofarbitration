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

public partial class DragDropPageViewModel : BaseViewModel
{
    private ObservableCollection<CompetitorModel> _competitors = new();
    private ObservableCollection<BracketRoundViewModel> _rounds = new();
    private BracketSlotViewModel? _draggedSlot;
    private bool _isDragging;

    
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

    #region Constructors
    public DragDropPageViewModel(IPopupService popupService, ILogger<DrawPageViewModel> logger, ResourceProvider resourceProvider, IRepository<DrawModel> repository)
        : base(logger, resourceProvider, popupService)
    {
    }
    #endregion

    public override async Task OnAppearing()
    {
        Competitors = new ObservableCollection<CompetitorModel>(new List<CompetitorModel>() 
        { 
            new CompetitorModel() { FirstName = "P1", LastName = "N1" },
            new CompetitorModel() { FirstName = "P2", LastName = "N2" },
            new CompetitorModel() { FirstName = "P3", LastName = "N3" },
            new CompetitorModel() { FirstName = "P4", LastName = "N4" },
            new CompetitorModel() { FirstName = "P5", LastName = "N5" },
            new CompetitorModel() { FirstName = "P6", LastName = "N6" },
            new CompetitorModel() { FirstName = "P7", LastName = "N7" },
            new CompetitorModel() { FirstName = "P8", LastName = "N8" },
            new CompetitorModel() { FirstName = "P9", LastName = "N9" },
            new CompetitorModel() { FirstName = "P10", LastName = "N10" },
            new CompetitorModel() { FirstName = "P11", LastName = "N11" },
            new CompetitorModel() { FirstName = "P12", LastName = "N12" },
            new CompetitorModel() { FirstName = "P13", LastName = "N13" },
            new CompetitorModel() { FirstName = "P14", LastName = "N14" },
            new CompetitorModel() { FirstName = "P15", LastName = "N15" },
            new CompetitorModel() { FirstName = "P16", LastName = "N16" },
            new CompetitorModel() { FirstName = "P17", LastName = "N17" }
        });

        InitializeBracket();
    }

    private void InitializeBracket()
    {
        var newRounds = new ObservableCollection<BracketRoundViewModel>();

        int n = Competitors.Count;
        int m = 1;
        while (m < n) m *= 2;
        if (m < 2) m = 2; // Min 1 match

        // Prepare Round 1 matches
        var round1 = new BracketRoundViewModel { Name = "Round 1" };
        var matchesInRound = m / 2;

        for (int i = 0; i < matchesInRound; i++)
        {
            var match = new BracketMatchViewModel();
            match.Slot1.Competitor = (i * 2 < n) ? Competitors[i * 2] : null;
            match.Slot2.Competitor = (i * 2 + 1 < n) ? Competitors[i * 2 + 1] : null;
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
        //UpdateCategoryCompetitors();

        DraggedSlot = null;

        // Notify changes to ensure UI refresh on all platforms
        OnPropertyChanged(nameof(Rounds));
    }

}