#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Models.Helpers;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoryPopupViewModel : BaseViewModel, IQueryAttributable
{
    #region Attributs
    private CategoryModel? _category;
    private CompetitionType _competitionType = CompetitionType.None;

    private LocalizedEnum<CategoryType>? _selectedType;
    private LocalizedEnum<RoundType>? _selectedRoundType;
    private LocalizedEnum<Genre>? _selectedGenre;
    private AgeRangeModel? _selectedAgeRange;
    private double? _weightMin = null;
    private double? _weightMax = null;
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit le type de compétition
    /// </summary>
    public CompetitionType CompetitionType
    {
        get => _competitionType;
        set
        {
            if (SetProperty(ref _competitionType, value))
            {
                UpdateCategoryTypes();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit la catégorie
    /// </summary>
    public CategoryModel? Category
    {
        get => _category;
        set
        {
            SetProperty(ref _category, value);

            if (value != null)
            {
                Title = Resources.UPDATE_CATEGORY;
                if (value.Competition != null && value.Competition.Type != CompetitionType.None)
                {
                    CompetitionType = value.Competition.Type;
                }
                UpdateCategoryTypes();
                SelectedType = CategoryTypes.FirstOrDefault(x => x.Value == value.Type);
                SelectedRoundType = RoundTypes.FirstOrDefault(x => x.Value == value.RoundType);
                SelectedGenre = Genres.FirstOrDefault(x => x.Value == value.Genre);
                SelectedAgeRange = AgeRanges.FirstOrDefault(x => x.Id == value.AgeRange?.Id);
                WeightMin = (value.WeightMin > 0) ? value.WeightMin : null;
                WeightMax = (value.WeightMax > 0) ? value.WeightMax : null;
            }
        }
    }

    /// <summary>
    /// Obtient ou définit le type de la catégorie
    /// </summary>
    public LocalizedEnum<CategoryType>? SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                ValidateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit le type de round
    /// </summary>
    public LocalizedEnum<RoundType>? SelectedRoundType
    {
        get => _selectedRoundType;
        set
        {
            if (SetProperty(ref _selectedRoundType, value))
            {
                ValidateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit le genre de la catégorie
    /// </summary>
    public LocalizedEnum<Genre>? SelectedGenre
    {
        get => _selectedGenre;
        set
        {
            if (SetProperty(ref _selectedGenre, value))
            {
                ValidateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit la tranche d'age de la catégorie
    /// </summary>
    public AgeRangeModel? SelectedAgeRange
    {
        get => _selectedAgeRange;
        set
        {
            if (SetProperty(ref _selectedAgeRange, value))
            {
                ValidateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient une valeur indiquant si les bornes de poids sont invalides (ex: poids min supérieur ou égal au poids max)
    /// </summary>
    public bool HasWeightError
    {
        get
        {
            if (WeightMin.HasValue && WeightMin.Value < 0) return true;
            if (WeightMax.HasValue && WeightMax.Value < 0) return true;
            if (WeightMin.HasValue && WeightMax.HasValue && WeightMin.Value > 0 && WeightMax.Value > 0)
            {
                return WeightMin.Value >= WeightMax.Value;
            }
            return false;
        }
    }

    /// <summary>
    /// Obtient ou définit le poids minimum de la catégorie
    /// </summary>
    public double? WeightMin
    {
        get => _weightMin;
        set
        {
            if (SetProperty(ref _weightMin, value))
            {
                OnPropertyChanged(nameof(HasWeightError));
                ValidateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit le poids maximum de la catégorie
    /// </summary>
    public double? WeightMax
    {
        get => _weightMax;
        set
        {
            if (SetProperty(ref _weightMax, value))
            {
                OnPropertyChanged(nameof(HasWeightError));
                ValidateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Obtient la liste des types de catégorie
    /// </summary>
    public ObservableCollection<LocalizedEnum<CategoryType>> CategoryTypes { get; } = new();

    /// <summary>
    /// Obtient la liste des type de round
    /// </summary>
    public List<LocalizedEnum<RoundType>> RoundTypes { get; }

    /// <summary>
    /// Obtient la liste des genres
    /// </summary>
    public List<LocalizedEnum<Genre>> Genres { get; }

    /// <summary>
    /// Obtient la liste des tranches d'age
    /// </summary>
    public List<AgeRangeModel> AgeRanges { get; }
    #endregion

    #region Constructors
    /// <summary>
    /// Constructeurs permettant d'initialiser les différents service
    /// </summary>
    /// <param name="logger">Service de log</param>
    /// <param name="popupService">Service de gestion des popups</param>
    /// <param name="resourceProvider">Service de traduction</param>
    public CategoryPopupViewModel(
        ILogger<CategoryPopupViewModel> logger,
        IPopupService popupService, 
        ResourceProvider resourceProvider
    )
    : base(logger, resourceProvider, popupService)
    {
        Title = resourceProvider.NEW_CATEGORY;

        // Initialisation des listes traduites via le manager global
        RoundTypes = LocalizeEnum<RoundType>("ENUM_ROUND_");
        Genres = LocalizeEnum<Genre>("ENUM_GENRE_");
        AgeRanges = AgeRangeModel.DefaultRanges;

        UpdateCategoryTypes();

        // Valeurs par défaut
        SelectedType = CategoryTypes.FirstOrDefault(x => x.Value == CategoryType.None);
        SelectedRoundType = RoundTypes.FirstOrDefault(x => x.Value == RoundType.None);
        SelectedGenre = Genres.FirstOrDefault(x => x.Value == Genre.None);
        SelectedAgeRange = AgeRanges.FirstOrDefault();
    }
    #endregion

    #region Implement IQueryAttributable
    /// <summary>
    /// Méthode permettant de récupérer les paramétres
    /// </summary>
    /// <param name="query"></param>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(nameof(CompetitionType)))
        {
            CompetitionType = (CompetitionType)query[nameof(CompetitionType)];
        }
        else if (query.ContainsKey(nameof(CompetitionType)))
        {
            CompetitionType = (CompetitionType)query[nameof(CompetitionType)];
        }

        if (query.ContainsKey(nameof(CategoryPopupViewModel.Category)))
        {
            Category = (CategoryModel?)query[nameof(CategoryPopupViewModel.Category)];
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Méthode permettant de filtrer les types de catégories suivant le type de la compétition
    /// </summary>
    private void UpdateCategoryTypes()
    {
        var currentSelectedValue = SelectedType?.Value ?? _category?.Type ?? CategoryType.None;
        var allTypes = LocalizeEnum<CategoryType>("ENUM_CATEGORY_");
        List<LocalizedEnum<CategoryType>> filtered;

        if (CompetitionType == CompetitionType.Taolu)
        {
            filtered = allTypes.Where(t => 
                t.Value == CategoryType.None 
                || t.Value == CategoryType.Taolu 
                || t.Value == CategoryType.TaoluModerneNord 
                || t.Value == CategoryType.TaoluModerneSud
            ).ToList();
        }
        else if (CompetitionType == CompetitionType.Sanda)
        {
            filtered = allTypes.Where(t => 
                t.Value == CategoryType.None 
                || t.Value == CategoryType.Sanda 
                || t.Value == CategoryType.SandaLight 
                || t.Value == CategoryType.SandaWushu 
                || t.Value == CategoryType.SandaTradi
            ).ToList();
        }
        else
        {
            filtered = allTypes;
        }

        CategoryTypes.Clear();
        foreach (var item in filtered)
        {
            CategoryTypes.Add(item);
        }

        SelectedType = CategoryTypes.FirstOrDefault(x => x.Value == currentSelectedValue)
            ?? CategoryTypes.FirstOrDefault(x => x.Value == CategoryType.None)
            ?? CategoryTypes.FirstOrDefault();
    }

    /// <summary>
    /// Méthode permettant de récupérer le résultat de l'étape
    /// </summary>
    /// <returns></returns>
    public CategoryModel GetResult()
    {
        if (SelectedRoundType == null || SelectedRoundType.Value == RoundType.None)
        {
            SelectedRoundType = RoundTypes.FirstOrDefault(x => x.Value == RoundType.Order);
        }

        return new CategoryModel
        {
            Id = Category?.Id ?? 0,
            Type = SelectedType?.Value ?? CategoryType.None,
            RoundType = SelectedRoundType?.Value ?? RoundType.Order,
            Genre = SelectedGenre?.Value ?? Genre.Mixte,
            AgeRangeId = SelectedAgeRange?.Id,
            AgeRange = SelectedAgeRange,
            WeightMin = WeightMin ?? 0,
            WeightMax = WeightMax ?? 0,
            Competition = null!
        };
    }
    #endregion

    #region Commands
    /// <summary>
    /// Commande permettant de fermer la fenêtre
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task Close()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }

    /// <summary>
    /// Méthode permettant de valider l'étape
    /// </summary>
    /// <returns></returns>
    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task Validate()
    {
        await _popupService.ClosePopupAsync<CategoryModel>(Shell.Current, GetResult());
    }

    /// <summary>
    /// Méthode permettant de rendre la validation possible ou non
    /// </summary>
    /// <returns></returns>
    private bool CanValidate()
    {
        var result = SelectedType != null && SelectedType.Value != CategoryType.None;
        result = result && (SelectedGenre != null && SelectedGenre.Value != Genre.None);
        result = result && (SelectedAgeRange != null && SelectedAgeRange.Id > 0);

        bool isSanda = SelectedType != null && (
            SelectedType.Value == CategoryType.Sanda || 
            SelectedType.Value == CategoryType.SandaLight || 
            SelectedType.Value == CategoryType.SandaWushu || 
            SelectedType.Value == CategoryType.SandaTradi
        );

        result = result && (!isSanda || (SelectedRoundType != null && SelectedRoundType.Value != RoundType.None));
        result = result && !HasWeightError;

        return result;
    }
    #endregion
}
