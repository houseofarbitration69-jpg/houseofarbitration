using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition;

public abstract class WizardStepViewModel : ObservableObject
{
    private bool _isValid;
    public bool IsValid 
    { 
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    public abstract string Title { get; }
}

public class WizardViewModel : BaseViewModel, INotifyPropertyChanged
{
    private int _currentStepIndex;
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

    public WizardStepViewModel? CurrentStep => (Steps != null && CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count) ? Steps[CurrentStepIndex] : null;
    public bool IsLastStep => Steps.Count > 0 && CurrentStepIndex == Steps.Count - 1;

    public ObservableCollection<WizardStepViewModel> Steps { get; } = new();

    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }

    public event Action<int>? ScrollToRequested;

    public WizardViewModel(ILogger<WizardViewModel> logger, ResourceProvider resourceProvider) : base(logger, resourceProvider)
    {
        NextCommand = new Command(async () => await GoNext(), () => CurrentStep?.IsValid ?? false);
        PreviousCommand = new Command(async () => await GoPrevious(), () => CurrentStepIndex > 0);
    }

    public void AddStep(WizardStepViewModel step)
    {
        step.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WizardStepViewModel.IsValid))
            {
                ((Command)NextCommand).ChangeCanExecute();
            }
        };
        Steps.Add(step);
        
        ((Command)NextCommand).ChangeCanExecute();
        ((Command)PreviousCommand).ChangeCanExecute();
    }

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
            await Shell.Current.DisplayAlertAsync("Wizard", "Information processed!", "OK");
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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}