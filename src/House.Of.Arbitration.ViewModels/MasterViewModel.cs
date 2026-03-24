#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class MasterViewModel : BaseViewModel
{
    #region Services
    private readonly IRepository<CompetitionModel> _repository;
    #endregion

    #region Attributs
    private string _title = String.Empty;
    private ObservableCollection<CompetitionModel>? _competitions;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    public ObservableCollection<CompetitionModel>? Competitions
    {
        get => _competitions;
        set => SetProperty(ref _competitions, value);
    }
    #endregion

    #region Constructors
    public MasterViewModel(ILogger<MasterViewModel> logger, ResourceProvider resourceProvider, IRepository<CompetitionModel> repository, IPopupService popupService) : base(logger, resourceProvider, popupService)
    {
        Title = resourceProvider.APPLICATION_NAME;
        _repository = repository;
    }
    #endregion

    public override async Task OnAppearing()
    {
        var data = await _repository.GetAllAsync(c => c.Categories);
        Competitions = new ObservableCollection<CompetitionModel>(data ?? new List<CompetitionModel>());
        await base.OnAppearing();
    }

    #region Commands
    [RelayCommand]
    private async Task EditCompetition(CompetitionModel competition)
    {
        if (competition == null) return;
        
        // Navigation vers le wizard en passant l'ID
        // On utilise un dictionnaire de paramètres pour Shell
        var navigationParameter = new Dictionary<string, object>
        {
            { "CompetitionId", competition.Id }
        };
        
        await Shell.Current.GoToAsync("CompetitionWizard", navigationParameter);
    }

    [RelayCommand]
    private async Task DeleteCompetition(CompetitionModel competition)
    {
        if (competition == null) return;

        bool confirm = await DisplayConfirmation(
            Resources.CONFIRM_DELETE,
            Resources.DELETE_COMPETITION_MESSAGE,
            Resources.YES,
            Resources.NO);

        if (confirm)
        {
            await _repository.DeleteAsync(competition);
            Competitions?.Remove(competition);
        }
    }

    [RelayCommand]
    private async Task StartCompetition(CompetitionModel competition)
    {
        if (competition == null) return;
        await Shell.Current.DisplayAlertAsync("Démarrage", $"Lancement de : {competition.Name}", "OK");
    }

    [RelayCommand]
    private async Task Create()
    {
        await Shell.Current.GoToAsync("CompetitionWizard");
    }
    #endregion
}
