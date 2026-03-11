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

public partial class CategoryPopupViewModel : BaseViewModel, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private CategoryModel? _category;

    private LocalizedEnum<CategoryType>? _selectedType;
    private LocalizedEnum<RoundType>? _selectedRoundType;
    private LocalizedEnum<Genre>? _selectedGenre;
    private LocalizedEnum<AgeRange>? _selectedAgeRange;
    private int _weightMin = 0;
    private int _weightMax = 0;
    #endregion

    #region Properties    
    public CategoryModel? Category
    {
        get => _category;
        set
        {
            SetProperty(ref _category, value);

            if (value != null)
            {
                SelectedType = CategoryTypes.FirstOrDefault(x => x.Value == value.Type);
                SelectedRoundType = RoundTypes.FirstOrDefault(x => x.Value == value.RoundType);
                SelectedGenre = Genres.FirstOrDefault(x => x.Value == value.Genre);
                SelectedAgeRange = AgeRanges.FirstOrDefault(x => x.Value == value.AgeRange);
                WeightMin = value.WeightMin;
                WeightMax = value.WeightMax;
            }
        }
    }

    public LocalizedEnum<CategoryType>? SelectedType
    {
        get => _selectedType;
        set => SetProperty(ref _selectedType, value);
    }

    public LocalizedEnum<RoundType>? SelectedRoundType
    {
        get => _selectedRoundType;
        set => SetProperty(ref _selectedRoundType, value);
    }

    public LocalizedEnum<Genre>? SelectedGenre
    {
        get => _selectedGenre;
        set => SetProperty(ref _selectedGenre, value);
    }

    public LocalizedEnum<AgeRange>? SelectedAgeRange
    {
        get => _selectedAgeRange;
        set => SetProperty(ref _selectedAgeRange, value);
    }

    public int WeightMin
    {
        get => _weightMin;
        set => SetProperty(ref _weightMin, value);
    }

    public int WeightMax
    {
        get => _weightMax;
        set => SetProperty(ref _weightMax, value);
    }

    public List<LocalizedEnum<CategoryType>> CategoryTypes { get; }

    public List<LocalizedEnum<RoundType>> RoundTypes { get; }

    public List<LocalizedEnum<Genre>> Genres { get; }

    public List<LocalizedEnum<AgeRange>> AgeRanges { get; }
    #endregion

    #region Constructors
    public CategoryPopupViewModel(IPopupService popupService, ILogger<CategoryPopupViewModel> logger, ResourceProvider resourceProvider)
        : base(logger, resourceProvider)
    {
        _popupService = popupService;

        // Initialisation des listes traduites via le manager global
        CategoryTypes = LocalizeEnum<CategoryType>("ENUM_CATEGORY_");
        RoundTypes = LocalizeEnum<RoundType>("ENUM_ROUND_");
        Genres = LocalizeEnum<Genre>("ENUM_GENRE_");
        AgeRanges = LocalizeEnum<AgeRange>("ENUM_AGE_");

        // Valeurs par défaut
        SelectedType = CategoryTypes.FirstOrDefault(x => x.Value == CategoryType.Sanda);
        SelectedRoundType = RoundTypes.FirstOrDefault(x => x.Value == RoundType.Elimination);
        SelectedGenre = Genres.FirstOrDefault(x => x.Value == Genre.Men);
        SelectedAgeRange = AgeRanges.FirstOrDefault(x => x.Value == AgeRange.Seniors);
    }
    #endregion

    #region Implement IQueryAttributable
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
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

    public CategoryModel GetResult()
    {
        return new CategoryModel
        {
            Id = Category?.Id ?? 0,
            Type = SelectedType?.Value ?? CategoryType.None,
            RoundType = SelectedRoundType?.Value ?? RoundType.None,
            Genre = SelectedGenre?.Value ?? Genre.None,
            AgeRange = SelectedAgeRange?.Value ?? AgeRange.None,
            WeightMin = WeightMin,
            WeightMax = WeightMax,
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

    [RelayCommand]
    private async Task Validate()
    {
        await _popupService.ClosePopupAsync<CategoryModel>(Shell.Current, GetResult());
    }
    #endregion
}
