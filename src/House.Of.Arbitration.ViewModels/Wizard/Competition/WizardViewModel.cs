using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition;

public class WizardViewModel<T> : BaseViewModel, INotifyPropertyChanged where T : class, new()
{
    #region Events
    public event Action<int>? ScrollToRequested;
    #endregion

    #region Services
    private readonly IRepository<T> _repository;
    #endregion

    #region Attributs
    private int _currentStepIndex;
    private string _name = String.Empty;
    private T _model = new();
    #endregion

    #region Properties
    public T Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }
    /// <summary>
    /// 
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// 
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
            
            //if (CurrentStep is SummaryStepViewModel summary)
            //{
            //    summary.Refresh();
            //}

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
    public WizardViewModel(ILogger<WizardViewModel<T>> logger, ResourceProvider resourceProvider, IRepository<T> repository) : base(logger, resourceProvider)
    {
        _repository = repository;

        NextCommand = new Command(async () => await GoNext(), () => CurrentStep?.IsValid ?? false);
        PreviousCommand = new Command(async () => await GoPrevious(), () => CurrentStepIndex > 0);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 
    /// </summary>
    /// <param name="step"></param>
    public void AddStep(WizardStepViewModel<T> step)
    {
        step.Model = Model; // Partager le modèle
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
    /// 
    /// </summary>
    /// <returns></returns>
    private async Task GoNext()
    {
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
        // Logique de validation et traitement final
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
            await _repository.AddAsync(Model);

            await Shell.Current.DisplayAlertAsync("Validation", "La compétition a été validée avec succès !", "OK");
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlertAsync("Erreur", "Certaines étapes contiennent des informations invalides.", "OK");
        }
    }

    /// <summary>
    /// 
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
    #endregion
}