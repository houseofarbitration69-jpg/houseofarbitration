#region Imports
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class SettingsStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Attributs
    private string _name = String.Empty;
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Now);
    #endregion

    #region Properties
    public override string Title => String.Empty;

    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value);

            if (Model != null)
            {
                Model.Name = value;
            }

            Validate();
        }
    }

    public DateOnly Date
    {
        get => _date;
        set
        {
            SetProperty(ref _date, value);

            if (Model != null)
            {
                Model.Date = value;
            }

            Validate();
        }
    }
    #endregion

    #region Override Methods
    protected override void OnModelUpdated(Models.CompetitionModel value)
    {
        if (value != null)
        {
            Name = value.Name;
            Date = value.Date;
            Validate();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 
    /// </summary>
    private void Validate()
    {
        IsValid = !string.IsNullOrWhiteSpace(Name);
    }
    #endregion
}
