using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using House.Of.Arbitration.Localization;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoryPopupViewModel : BaseViewModel
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private CategoryType _selectedType = CategoryType.Sanda;
    private RoundType _selectedRoundType = RoundType.Elimination;
    private Genre _selectedGenre = Genre.Men;
    private AgeRange _selectedAgeRange = AgeRange.Seniors;
    private int _weightMin = 0;
    private int _weightMax = 0;
    #endregion

    #region Properties    
    public CategoryType SelectedType
    {
        get => _selectedType;
        set => SetProperty(ref _selectedType, value);
    }

    public RoundType SelectedRoundType
    {
        get => _selectedRoundType;
        set => SetProperty(ref _selectedRoundType, value);
    }

    public Genre SelectedGenre
    {
        get => _selectedGenre;
        set => SetProperty(ref _selectedGenre, value);
    }
    
    public AgeRange SelectedAgeRange
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

    public List<CategoryType> CategoryTypes => Enum.GetValues(typeof(CategoryType)).Cast<CategoryType>().ToList();
    
    public List<RoundType> RoundTypes => Enum.GetValues(typeof(RoundType)).Cast<RoundType>().ToList();
    
    public List<Genre> Genres => Enum.GetValues(typeof(Genre)).Cast<Genre>().ToList();
    
    public List<AgeRange> AgeRanges => Enum.GetValues(typeof(AgeRange)).Cast<AgeRange>().ToList();
    #endregion

    #region Constructors
    public CategoryPopupViewModel(IPopupService popupService, ILogger<CategoryPopupViewModel> logger, ResourceProvider resourceProvider) 
        : base(logger, resourceProvider)
    {
        _popupService = popupService;
    }
    #endregion

    #region Private Methods
    private CategoryModel GetResult()
    {
        return new CategoryModel
        {
            Type = SelectedType,
            RoundType = SelectedRoundType,
            Genre = SelectedGenre,
            AgeRange = SelectedAgeRange,
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
