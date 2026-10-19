#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition;

[QueryProperty(nameof(CompetitionId), Constants.Navigation.Parameter.COMPETITION_ID)]
public partial class WizardViewModel<T> : BaseViewModel, INotifyPropertyChanged where T : class, new()
{
    #region Events
    public event Action<int>? ScrollToRequested;
    #endregion

    #region Services
    /// <summary>
    /// Service permettant l'accès aux données
    /// </summary>
    protected readonly IRepository<T> _repository;
    #endregion

    #region Attributs
    private int _currentStepIndex;
    private T _model = new();
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit le nom de compétition
    /// </summary>
    [ObservableProperty]
    public string _name = String.Empty;

    /// <summary>
    /// Obtient ou définit l'identifiant de la compétition
    /// </summary>
    [ObservableProperty]
    public int _competitionId;

    /// <summary>
    /// Obtient ou définit le model a afficher
    /// </summary>
    public T Model
    {
        get => _model;
        set
        {
            if (SetProperty(ref _model, value))
            {
                // PROPAGATION CRUCIALE : on parcourt toutes les étapes
                if (Steps != null)
                {
                    foreach (var step in Steps)
                    {
                        step.Model = value;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Obtient ou définit l'index de l'étape courante
    /// </summary>
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        set 
        { 
            if (_currentStepIndex == value) return;
            _currentStepIndex = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(IsLastStep));
            
            ((Command)NextCommand).ChangeCanExecute();
            ((Command)PreviousCommand).ChangeCanExecute();

            // Refresh step data when navigating to it
            MainThread.BeginInvokeOnMainThread(async () => await CurrentStep?.OnAppearing()!);
        }
    }

    /// <summary>
    /// Obtient si on est sur la dernière étape
    /// </summary>
    public bool IsLastStep => Steps.Count > 0 && CurrentStepIndex == Steps.Count - 1;

    /// <summary>
    /// Obtient la liste des étapes
    /// </summary>
    public ObservableCollection<WizardStepViewModel<T>> Steps { get; } = new();

    /// <summary>
    /// Obtient l'étape courante
    /// </summary>
    public WizardStepViewModel<T>? CurrentStep => (Steps != null && CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count) ? Steps[CurrentStepIndex] : null;
    #endregion

    #region Commands
    /// <summary>
    /// Commande permettant d'aller à l'étape suivante
    /// </summary>
    public ICommand NextCommand { get; }

    /// <summary>
    /// Commande permettant d'aller à l'étape précédente
    /// </summary>
    public ICommand PreviousCommand { get; }
    #endregion

    #region Constructors
    /// <summary>
    /// Constructeur permettant d'initialiser les différents service
    /// </summary>
    /// <param name="logger">Service de log</param>
    /// <param name="resourceProvider">Service de traduction</param>
    /// <param name="repository">Service d'accès aux données</param>
    /// <param name="popupService">Service de gestion des popups</param>
    public WizardViewModel(
        ILogger<WizardViewModel<T>> logger, 
        ResourceProvider resourceProvider, 
        IRepository<T> repository, 
        IPopupService popupService
    ) : base(logger, resourceProvider, popupService)
    {
        _repository = repository;

        NextCommand = new Command(async () => await GoNext(), () => CurrentStep?.IsValid ?? false);
        PreviousCommand = new Command(async () => await GoPrevious(), () => CurrentStepIndex > 0);
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override async Task OnAppearing()
    {
        await base.OnAppearing();
        if (CurrentStep != null)
        {
            await CurrentStep.OnAppearing();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Méthode permettant d'ajouter une étape
    /// </summary>
    /// <param name="step"></param>
    public void AddStep(WizardStepViewModel<T> step)
    {
        // On injecte le modèle actuel immédiatement
        step.Model = Model; 
        
        step.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WizardStepViewModel<T>.IsValid))
            {
                ((Command)NextCommand).ChangeCanExecute();
            }
        };
        Steps.Add(step);

        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsLastStep));
        ((Command)NextCommand).ChangeCanExecute();
        ((Command)PreviousCommand).ChangeCanExecute();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Méthode permettant d'aller a l'étape suivante
    /// </summary>
    /// <returns></returns>
    private async Task GoNext()
    {
        if(CurrentStep != null)
        {
            await CurrentStep.Save();
        }

        if (CurrentStepIndex < Steps.Count - 1)
        {
            int nextIndex = CurrentStepIndex + 1;
            ScrollToRequested?.Invoke(nextIndex);
            CurrentStepIndex = nextIndex;
        }
        else
        {
            await OnFinalizeAsync();
        }
    }

    /// <summary>
    /// Méthode permettant d'aller à l'étape précédente
    /// </summary>
    /// <returns></returns>
    private async Task GoPrevious()
    {
        if (CurrentStepIndex > 0)
        {
            int prevIndex = CurrentStepIndex - 1;
            ScrollToRequested?.Invoke(prevIndex);
            CurrentStepIndex = prevIndex;
        }
    }

    /// <summary>
    /// Méthode permettant de finaliser le wizard
    /// </summary>
    /// <returns></returns>
    private async Task OnFinalizeAsync()
    {
        bool isAllValid = true;
        foreach (var step in Steps)
        {
            if (!step.IsValid)
            {
                isAllValid = false;
                break;
            }
        }

        if (isAllValid)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlertAsync("Erreur", "Certaines étapes contiennent des informations invalides.", "OK");
        }
    }
    #endregion
}
