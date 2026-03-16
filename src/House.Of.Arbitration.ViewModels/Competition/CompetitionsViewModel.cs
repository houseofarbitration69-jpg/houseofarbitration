using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace House.Of.Arbitration.ViewModels.Competition;

public partial class CompetitionsViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<CompetitionModel> _repository;
    #endregion

    #region Attributs
    [ObservableProperty]
    private ObservableCollection<CompetitionModel>? _competitions;
    
    [ObservableProperty]
    private CompetitionModel? _selectedCompetition;
    
    [ObservableProperty]
    private bool _isPopupVisible;
    #endregion

    #region Constructors
    public CompetitionsViewModel(
        ILogger<CompetitionsViewModel> logger,
        ResourceProvider resourceProvider,
        IRepository<CompetitionModel> repository,
        IPopupService popupService)
        : base(logger, resourceProvider, popupService)
    {
        _repository = repository;
    }
    #endregion

    #region Commands
    [RelayCommand]
    private void ShowDetails(CompetitionModel? competition)
    {
        if (competition == null) return;
        SelectedCompetition = competition;
        IsPopupVisible = true;
    }

    [RelayCommand]
    private void ClosePopup()
    {
        IsPopupVisible = false;
        SelectedCompetition = null;
    }

    [RelayCommand]
    private async Task StartCompetition(CompetitionModel? competition)
    {
        if (competition == null) return;
        await Shell.Current.DisplayAlertAsync("Démarrage", $"Lancement de la compétition : {competition.Name}", "OK");
    }

    [RelayCommand]
    private async Task Create()
    {
        await Shell.Current.GoToAsync("WizardPage");
    }
    #endregion

    #region Override Methods
    public override async Task OnAppearing()
    {
        var data = await _repository.GetAllAsync(c => c.Categories);
        Competitions = new ObservableCollection<CompetitionModel>(data ?? new List<CompetitionModel>());
        await base.OnAppearing();
    }
    #endregion
}
