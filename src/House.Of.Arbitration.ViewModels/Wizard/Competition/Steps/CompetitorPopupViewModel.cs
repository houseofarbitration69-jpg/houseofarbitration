#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
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
    private readonly IRepository<CompetitorModel> _repository;
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
    private List<string> _clubs = new();
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit le compétiteur
    /// </summary>
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
                Weight = value.Weight;
            }
        }
    }

    /// <summary>
    /// Obtient ou définit la catégorie courante
    /// </summary>
    public CategoryModel? Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    /// <summary>
    /// Obtient ou définit le prénom du compétiteur
    /// </summary>
    public string FirstName
    {
        get => _firstName;
        set
        {
            SetProperty(ref _firstName, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Obtient ou définit le nom du compétiteur
    /// </summary>
    public string LastName
    {
        get => _lastName;
        set
        {
            SetProperty(ref _lastName, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Obtient ou définit le club du compétiteur
    /// </summary>
    public string Club
    {
        get => _club;
        set
        {
            SetProperty(ref _club, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Obtient ou définit la date de naissance du compétiteur
    /// </summary>
    public DateTime? BirthDate
    {
        get => _birthDate;
        set
        {
            SetProperty(ref _birthDate, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Obtient ou définit le poids du compétiteur
    /// </summary>
    public double Weight
    {
        get => _weight;
        set
        {
            SetProperty(ref _weight, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Obtient ou définit le genre du compétiteur
    /// </summary>
    public LocalizedEnum<Genre>? SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            SetProperty(ref _selectedGenre, value);
            ValidateCommand.NotifyCanExecuteChanged();
        }
    }
    /// <summary>
    /// Obtient ou définit la liste des genres
    /// </summary>
    public List<LocalizedEnum<Genre>> Genres { get; }
    
    /// <summary>
    /// Obtient ou définit la liste des clubs déjà présent dans la base
    /// </summary>
    public List<string> Clubs
    {
        get => _clubs;
        set => SetProperty(ref _clubs, value);
    }
    #endregion

    #region Constructor
    public CompetitorPopupViewModel(IPopupService popupService, ILogger<CompetitorPopupViewModel> logger, ResourceProvider resourceProvider, IRepository<CompetitorModel> repository)
        : base(logger, resourceProvider, popupService)
    {
        _repository = repository;

        Genres = LocalizeEnum<Genre>("ENUM_GENRE_");
        InitData();
    }
    #endregion

    #region Implement IQueryAttributable
    /// <summary>
    /// Méthode permettant de récupérer les paramètres
    /// </summary>
    /// <param name="query"></param>
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

    #region Init Methods
    /// <summary>
    /// Méthode permettant l'initialisation des données
    /// </summary>
    private async void InitData()
    {
        // Récupération de la liste des clubs
        Clubs = (await _repository.GetAllAsync())?.Select(c => c.Club).Distinct()?.ToList() ?? new();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Méthode permettant de récupérer les données saisie par l'utilisateur
    /// </summary>
    /// <returns></returns>
    public CompetitorModel GetResult()
    {
        var competitor = new CompetitorModel
        {
            Id = Competitor?.Id ?? 0,
            LastName = LastName,
            FirstName = FirstName,
            Genre = SelectedGenre?.Value ?? Genre.None,
            Club = Club,
            BirthDate = BirthDate ?? DateTime.Now,
            Weight = Weight
        };

        if (Category != null)
        {
            competitor.Categories = new List<CategoryModel> { Category };
        }

        return competitor;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Méthode permettant de traduire les enumétrations
    /// </summary>
    /// <typeparam name="T">Type de l'énumération</typeparam>
    /// <param name="prefix"></param>
    /// <returns></returns>
    private List<LocalizedEnum<T>> LocalizeEnum<T>(string prefix) where T : Enum
    {
        return Enum.GetValues(typeof(T))
            .Cast<T>()
            .Select(e => new LocalizedEnum<T>(e, LocalizationResourceManager.Instance.GetValue($"{prefix}{e.ToString().ToUpper()}")))
            .ToList();
    }
    #endregion

    #region Commands
    /// <summary>
    /// Commande permettant de fermer la popup sans sauvegarder les données
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task Close()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }

    /// <summary>
    /// Méthode permettant de fermer la popup en sauvegardant les données saisies
    /// </summary>
    /// <returns></returns>
    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task Validate()
    {
        await _popupService.ClosePopupAsync<CompetitorModel>(Shell.Current, GetResult());
    }

    /// <summary>
    /// Méthode permettant de valider si les données sont correct
    /// </summary>
    /// <returns></returns>
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
