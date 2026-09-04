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

    private string _title = String.Empty;

    private LocalizedEnum<CategoryType>? _selectedType;
    private LocalizedEnum<RoundType>? _selectedRoundType;
    private LocalizedEnum<Genre>? _selectedGenre;
    private AgeRangeModel? _selectedAgeRange;
    private int? _weightMin = null;
    private int? _weightMax = null;
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
                WeightMin = value.WeightMin;
                WeightMax = value.WeightMax;
            }
        }
    }

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

    public int? WeightMin
    {
        get => _weightMin;
        set => SetProperty(ref _weightMin, value);
    }

    public int? WeightMax
    {
        get => _weightMax;
        set => SetProperty(ref _weightMax, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ObservableCollection<LocalizedEnum<CategoryType>> CategoryTypes { get; } = new();

    public List<LocalizedEnum<RoundType>> RoundTypes { get; }

    public List<LocalizedEnum<Genre>> Genres { get; }

    public List<AgeRangeModel> AgeRanges { get; }
    #endregion

    #region Constructors
    public CategoryPopupViewModel(IPopupService popupService, ILogger<CategoryPopupViewModel> logger, ResourceProvider resourceProvider)
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
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(nameof(CompetitionType)))
        {
            CompetitionType = (CompetitionType)query[nameof(CompetitionType)];
        }
        else if (query.ContainsKey("CompetitionType"))
        {
            CompetitionType = (CompetitionType)query["CompetitionType"];
        }

        if (query.ContainsKey(nameof(CategoryPopupViewModel.Category)))
        {
            Category = (CategoryModel?)query[nameof(CategoryPopupViewModel.Category)];
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

    private void UpdateCategoryTypes()
    {
        var currentSelectedValue = SelectedType?.Value ?? _category?.Type ?? CategoryType.None;
        var allTypes = LocalizeEnum<CategoryType>("ENUM_CATEGORY_");
        List<LocalizedEnum<CategoryType>> filtered;

        if (CompetitionType == CompetitionType.Taolu)
        {
            filtered = allTypes.Where(t => t.Value == CategoryType.None || t.Value == CategoryType.Taolu || t.Value == CategoryType.TaoluModerneNord || t.Value == CategoryType.TaoluModerneSud).ToList();
        }
        else if (CompetitionType == CompetitionType.Sanda)
        {
            filtered = allTypes.Where(t => t.Value == CategoryType.None || t.Value == CategoryType.Sanda || t.Value == CategoryType.SandaLight || t.Value == CategoryType.SandaWushu || t.Value == CategoryType.SandaTradi).ToList();
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
    [RelayCommand]
    private async Task Close()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }

    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task Validate()
    {
        await _popupService.ClosePopupAsync<CategoryModel>(Shell.Current, GetResult());
    }

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

        return result;
    }
    #endregion
}
