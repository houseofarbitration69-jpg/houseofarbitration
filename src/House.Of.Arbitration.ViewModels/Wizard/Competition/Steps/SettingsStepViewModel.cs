using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.ViewModels.Wizard.Competition;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class SettingsStepViewModel : WizardStepViewModel
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private DateTime _date = DateTime.Now;

    [ObservableProperty]
    private string _place;

    public override string Title => "Settings";

    partial void OnNameChanged(string value) => Validate();
    partial void OnDateChanged(DateTime value) => Validate();
    partial void OnPlaceChanged(string value) => Validate();

    private void Validate()
    {
        // Simple validation logic
        IsValid = !string.IsNullOrWhiteSpace(Name) && 
                  !string.IsNullOrWhiteSpace(Place) && 
                  Date >= DateTime.Today;
    }
}
