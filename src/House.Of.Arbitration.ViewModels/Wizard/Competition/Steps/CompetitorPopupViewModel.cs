#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CompetitorPopupViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private CategoryModel? _category;
    private CompetitorModel _competitor = new();
    private LocalizedEnum<Genre>? _selectedGenre;
    private string _firstName = String.Empty;
    private string _lastName = String.Empty;
    private string _club = String.Empty;
    private DateTime? _birthDate;
    private double _weight;
    #endregion

    #region Properties
    public CompetitorModel Competitor
    {
        get => _competitor;
        set
        {
            SetProperty(ref _competitor, value);
            if (value != null)
            {
                SelectedGenre = Genres.FirstOrDefault(x => x.Value == value.Genre);
                FirstName = value.FirstName;
                LastName = value.LastName;
                Club = value.Club;
                BirthDate = value.BirthDate;
                Weight = value.CurrentWeight;
            }
        }
    }

    public CategoryModel? Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string FirstName
    {
        get => _firstName;
        set
        {
            SetProperty(ref _firstName, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public string LastName
    {
        get => _lastName;
        set
        {
            SetProperty(ref _lastName, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public string Club
    {
        get => _club;
        set
        {
            SetProperty(ref _club, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public DateTime? BirthDate
    {
        get => _birthDate;
        set
        {
            SetProperty(ref _birthDate, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public double Weight
    {
        get => _weight;
        set
        {
            SetProperty(ref _weight, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public LocalizedEnum<Genre>? SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            SetProperty(ref _selectedGenre, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    public List<LocalizedEnum<Genre>> Genres { get; }
    #endregion

    #region Constructor
    public CompetitorPopupViewModel(IPopupService popupService, ILogger<CompetitorPopupViewModel> logger, ResourceProvider resourceProvider)
        : base(logger, resourceProvider, popupService)
    {
        _popupService = popupService;
        Genres = LocalizeEnum<Genre>("ENUM_GENRE_");
    }
    #endregion

    #region Implement IQueryAttributable
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(nameof(Competitor)))
        {
            Competitor = (CompetitorModel)query[nameof(Competitor)];
        }

        if (query.ContainsKey(nameof(Category)))
        {
            Category = (CategoryModel)query[nameof(Category)];
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

    public CompetitorModel GetResult()
    {
        return new CompetitorModel
        {
            Id = Competitor?.Id ?? 0,
            LastName = LastName,
            FirstName = FirstName,
            Genre = SelectedGenre?.Value ?? Genre.None,
            Club = Club,
            BirthDate = BirthDate ?? DateTime.Now,
            CurrentWeight = Weight,
            CategoryId = Category?.Id ?? 0
        };
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Close()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }

    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task Validate()
    {
        await _popupService.ClosePopupAsync<CompetitorModel>(Shell.Current, GetResult());
    }

    private bool CanValidate()
    {
        var result = !String.IsNullOrEmpty(LastName);
        result = result && !String.IsNullOrEmpty(FirstName);
        result = result && (SelectedGenre != null && SelectedGenre.Value != Genre.None);
        result = result && !String.IsNullOrEmpty(Club);
        result = result && (BirthDate != null);

        return result;
    }
    #endregion
}
