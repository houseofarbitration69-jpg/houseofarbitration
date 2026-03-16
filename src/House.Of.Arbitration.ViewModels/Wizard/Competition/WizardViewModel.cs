#region Imports
using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition;

[QueryProperty(nameof(CompetitionId), "CompetitionId")]
public class WizardViewModel<T> : BaseViewModel, INotifyPropertyChanged where T : class, new()
{
    #region Events
    public event Action<int>? ScrollToRequested;
    #endregion

    #region Services
    protected readonly IRepository<T> _repository;
    #endregion

    #region Attributs
    private int _currentStepIndex;
    private string _name = String.Empty;
    private T _model = new();
    private int _competitionId;
    #endregion

    #region Properties
    public int CompetitionId
    {
        get => _competitionId;
        set => SetProperty(ref _competitionId, value);
    }

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

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

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
        }
    }

    public bool IsLastStep => Steps.Count > 0 && CurrentStepIndex == Steps.Count - 1;

    public ObservableCollection<WizardStepViewModel<T>> Steps { get; } = new();

    public WizardStepViewModel<T>? CurrentStep => (Steps != null && CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count) ? Steps[CurrentStepIndex] : null;
    #endregion

    #region Commands
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    #endregion

    #region Constructors
    public WizardViewModel(ILogger<WizardViewModel<T>> logger, ResourceProvider resourceProvider, IRepository<T> repository, IPopupService popupService) : base(logger, resourceProvider, popupService)
    {
        _repository = repository;

        NextCommand = new Command(async () => await GoNext(), () => CurrentStep?.IsValid ?? false);
        PreviousCommand = new Command(async () => await GoPrevious(), () => CurrentStepIndex > 0);
    }
    #endregion

    #region Public Methods
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
            //// Vérifier si c'est un ajout ou une mise à jour via réflexion
            //var idProp = Model.GetType().GetProperty("Id");
            //int id = idProp != null ? (int)idProp.GetValue(Model)! : 0;

            //if (id > 0)
            //    await _repository.UpdateAsync(Model);
            //else
            //    await _repository.AddAsync(Model);

            //await Shell.Current.DisplayAlertAsync("Validation", "La compétition a été enregistrée avec succès !", "OK");
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlertAsync("Erreur", "Certaines étapes contiennent des informations invalides.", "OK");
        }
    }

    private async Task GoPrevious()
    {
        if (CurrentStepIndex > 0)
        {
            int prevIndex = CurrentStepIndex - 1;
            ScrollToRequested?.Invoke(prevIndex);
            CurrentStepIndex = prevIndex;
        }
    }
    #endregion
}
