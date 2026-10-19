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

namespace House.Of.Arbitration.ViewModels;

public partial class MasterViewModel : BaseViewModel
{
    #region Services
    /// <summary>
    /// Service permettant d'acceder à la base de donnée
    /// </summary>
    private readonly IRepository<CompetitionModel> _repository;
    #endregion

    #region Attributs
    /// <summary>
    /// Obtient ou définit la liste des compétitions
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CompetitionModel>? _competitions;

    /// <summary>
    /// Ensemble des identifiants des compétitions en cours d'action
    /// </summary>
    private readonly HashSet<int> _busyCompetitionIds = new();
    #endregion

    #region Constructors
    /// <summary>
    /// Constructeur permettant d'initialiser les différents service
    /// </summary>
    /// <param name="logger">Service de log</param>
    /// <param name="resourceProvider">Service de traduction</param>
    /// <param name="repository">Service permettant d'accèder aux données</param>
    /// <param name="popupService">Service de gestion des popups</param>
    public MasterViewModel(
        ILogger<MasterViewModel> logger, 
        ResourceProvider resourceProvider, 
        IRepository<CompetitionModel> repository, 
        IPopupService popupService
    ) : base(logger, resourceProvider, popupService)
    {
        _title = resourceProvider.APPLICATION_NAME;
        _repository = repository;
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override async Task OnAppearing()
    {
        _busyCompetitionIds.Clear();
        NotifyCompetitionCommandsCanExecuteChanged();

        IsBusy = true;

        try
        {
            var data = await _repository.GetAllAsync(c => c.Categories);
            Competitions = new ObservableCollection<CompetitionModel>(data ?? new List<CompetitionModel>());
            await base.OnAppearing();
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Vérifie si une action peut être exécutée sur la compétition spécifiée
    /// </summary>
    /// <param name="competition">Compétition cible</param>
    /// <returns>True si aucune action n'est en cours sur cette compétition</returns>
    private bool CanExecuteCompetitionAction(CompetitionModel? competition)
    {
        if (competition == null) return false;
        return !_busyCompetitionIds.Contains(competition.Id);
    }

    /// <summary>
    /// Notifie le changement d'état d'exécution pour toutes les commandes de compétition
    /// </summary>
    private void NotifyCompetitionCommandsCanExecuteChanged()
    {
        EditCompetitionCommand.NotifyCanExecuteChanged();
        DrawsCommand.NotifyCanExecuteChanged();
        DeleteCompetitionCommand.NotifyCanExecuteChanged();
        StartCompetitionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Commande permettant d'editer une compétition
    /// </summary>
    /// <param name="competition">Compétition à modifier</param>
    /// <returns></returns>
    [RelayCommand(CanExecute = nameof(CanExecuteCompetitionAction), AllowConcurrentExecutions = true)]
    private async Task EditCompetition(CompetitionModel competition)
    {
        if (competition == null) return;
        if (!_busyCompetitionIds.Add(competition.Id)) return;
        NotifyCompetitionCommandsCanExecuteChanged();

        try
        {
            // Navigation vers le wizard en passant l'ID
            // On utilise un dictionnaire de paramètres pour Shell
            var navigationParameter = new Dictionary<string, object>
            {
                { Constants.Navigation.Parameter.COMPETITION_ID, competition.Id }
            };
            
            await Shell.Current.GoToAsync(Constants.Navigation.Page.COMPETITION_WIZARD, navigationParameter);
        }
        finally
        {
            _busyCompetitionIds.Remove(competition.Id);
            NotifyCompetitionCommandsCanExecuteChanged();
        }
    }

    /// <summary>
    /// Commande permettant de supprimer une compétition
    /// </summary>
    /// <param name="competition">Compétition a supprimer</param>
    /// <returns></returns>
    [RelayCommand(CanExecute = nameof(CanExecuteCompetitionAction), AllowConcurrentExecutions = true)]
    private async Task DeleteCompetition(CompetitionModel competition)
    {
        if (competition == null) return;
        if (!_busyCompetitionIds.Add(competition.Id)) return;
        NotifyCompetitionCommandsCanExecuteChanged();

        try
        {
            bool confirm = await DisplayConfirmation(
                Resources.CONFIRM_DELETE,
                Resources.DELETE_COMPETITION_MESSAGE,
                Resources.YES,
                Resources.NO);

            if (confirm)
            {
                IsBusy = true;
                try
                {
                    await _repository.DeleteAsync(competition);

                    var item = Competitions?.FirstOrDefault(c => c.Id == competition.Id);

                    if (item != null)
                    {
                        Competitions?.Remove(item);
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
        finally
        {
            _busyCompetitionIds.Remove(competition.Id);
            NotifyCompetitionCommandsCanExecuteChanged();
        }
    }

    /// <summary>
    /// Commande permettant d'afficher les tirages d'une compétition
    /// </summary>
    /// <param name="competition">Compétition dont on veut afficher les tirages</param>
    /// <returns></returns>
    [RelayCommand(CanExecute = nameof(CanExecuteCompetitionAction), AllowConcurrentExecutions = true)]
    private async Task Draws(CompetitionModel competition)
    {
        if (competition == null) return;
        if (!_busyCompetitionIds.Add(competition.Id)) return;
        NotifyCompetitionCommandsCanExecuteChanged();

        try
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { Constants.Navigation.Parameter.COMPETITION_ID, competition.Id }
            };

            await Shell.Current.GoToAsync(Constants.Navigation.Page.DRAWS_MANAGEMENT, navigationParameter);
        }
        finally
        {
            _busyCompetitionIds.Remove(competition.Id);
            NotifyCompetitionCommandsCanExecuteChanged();
        }
    }

    /// <summary>
    /// Commande permettant de démarrer une compétition
    /// </summary>
    /// <param name="competition">Compétition qu'on veut démarrer</param>
    /// <returns></returns>
    [RelayCommand(CanExecute = nameof(CanExecuteCompetitionAction), AllowConcurrentExecutions = true)]
    private async Task StartCompetition(CompetitionModel competition)
    {
        if (competition == null) return;
        if (!_busyCompetitionIds.Add(competition.Id)) return;
        NotifyCompetitionCommandsCanExecuteChanged();

        try
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { Constants.Navigation.Parameter.COMPETITION_ID, competition.Id }
            };

            if (competition.Type == CompetitionType.Taolu)
            {
                await Shell.Current.GoToAsync(Constants.Navigation.Page.SERVER_TAOLU, navigationParameter);
            }
            else
            {
                await Shell.Current.GoToAsync(Constants.Navigation.Page.SERVER_SANDA, navigationParameter);
            }
        }
        finally
        {
            _busyCompetitionIds.Remove(competition.Id);
            NotifyCompetitionCommandsCanExecuteChanged();
        }
    }

    /// <summary>
    /// Commande permettant de créer une compétition
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task Create()
    {
        await Shell.Current.GoToAsync(Constants.Navigation.Page.COMPETITION_WIZARD);
    }
    #endregion
}
