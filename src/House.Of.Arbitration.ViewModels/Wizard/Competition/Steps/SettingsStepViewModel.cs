#region Imports
using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
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
    private LocalizedEnum<CompetitionType>? _selectedType;
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

    public LocalizedEnum<CompetitionType>? SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                if (Model != null) Model.Type = value?.Value ?? CompetitionType.None;
                Validate();
            }
        }
    }

    public List<LocalizedEnum<CompetitionType>> CompetitionTypes { get; }
    #endregion

    #region Constructors
    public SettingsStepViewModel(ResourceProvider resourceProvider, IPopupService popupService, IRepository<CompetitionModel> repository) : base(resourceProvider, popupService)
    {
        _repository = repository;
        CompetitionTypes = LocalizeEnum<CompetitionType>("ENUM_COMPETITION_");
        SelectedType = CompetitionTypes.FirstOrDefault(x => x.Value == CompetitionType.None);
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
            _selectedType = CompetitionTypes.FirstOrDefault(x => x.Value == value.Type);

            // Notification explicite à l'UI
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Date));
            OnPropertyChanged(nameof(SelectedType));

            Validate();
        }
    }
    #endregion

    #region Private Methods
    private List<LocalizedEnum<T>> LocalizeEnum<T>(string prefix) where T : Enum
    {
        return Enum.GetValues(typeof(T))
            .Cast<T>()
            .Select(e => new LocalizedEnum<T>(e, LocalizationResourceManager.Instance.GetValue($"{prefix}{e.ToString().ToUpper()}")))
            .ToList();
    }

    private void Validate()
    {
        IsValid = !string.IsNullOrWhiteSpace(Name) && SelectedType != null && SelectedType.Value != CompetitionType.None;
    }
    #endregion
}
