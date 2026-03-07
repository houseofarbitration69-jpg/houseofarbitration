#region Imports
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class SettingsStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Attributs
    private string _name = String.Empty;
    private DateTime _date = DateTime.Now;
    #endregion

    #region Properties
    public override string Title => "Paramètres";

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                if (Model != null) Model.Name = value;
                Validate();
            }
        }
    }

    public DateTime Date
    {
        get => _date;
        set
        {
            if (SetProperty(ref _date, value))
            {
                if (Model != null) Model.Date = value;
                Validate();
            }
        }
    }
    #endregion

    #region Override Methods
    protected override void OnModelUpdated(CompetitionModel value)
    {
        if (value != null)
        {
            // Mise à jour des champs locaux sans déclencher de boucle infinie
            _name = value.Name;
            _date = value.Date;
            
            // Notification explicite à l'UI
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Date));
            
            Validate();
        }
    }
    #endregion

    #region Private Methods
    private void Validate()
    {
        IsValid = !string.IsNullOrWhiteSpace(Name);
    }
    #endregion
}
