#region Imports
using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class SettingsStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Services
    private readonly IRepository<CompetitionModel> _repository;
    #endregion

    #region Attributs
    private string _name = String.Empty;
    private DateTime _date = DateTime.Now;
    #endregion

    #region Properties
    public override string Title => Resources.SETTINGS;

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

    #region Constructors
    public SettingsStepViewModel(ResourceProvider resourceProvider, IPopupService popupService, IRepository<CompetitionModel> repository) : base(resourceProvider, popupService)
    {
        _repository = repository;
    }
    #endregion

    #region Override Methods
    public override async Task Save()
    {
        if (Model.Id > 0)
        {
            await _repository.UpdateAsync(Model);
        }
        else
        {
            await _repository.AddAsync(Model);
        }
    }

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
