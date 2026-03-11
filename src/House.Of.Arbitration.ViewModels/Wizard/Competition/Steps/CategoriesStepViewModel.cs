#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoriesStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private ObservableCollection<CategoryModel> _categories = new();
    #endregion

    #region Properties
    public override string Title => "Catégories";

    public ObservableCollection<CategoryModel> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }
    #endregion

    #region Constructors
    public CategoriesStepViewModel(IPopupService popupService, ResourceProvider resourceProvider) : base(resourceProvider)
    {
        _popupService = popupService;
        Categories.CollectionChanged += OnCategoriesCollectionChanged;
        Validate();
    }
    #endregion

    #region Private Methods
    private void OnCategoriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Model != null)
        {
            Model.Categories = Categories.ToList();
        }
        Validate();
    }

    private void RefreshCategory(CategoryModel category)
    {
        var index = Categories.IndexOf(category);
        if (index != -1)
        {
            Categories[index] = null!;
            Categories[index] = category;
        }
    }

    private void Validate()
    {
        IsValid = Categories != null && Categories.Count > 0;
    }
    #endregion

    #region Override Methods

    #endregion

    #region Commands
    [RelayCommand]
    private async Task AddCategory()
    {
        // On passe une action vide pour satisfaire la signature de la méthode
        var result = await _popupService.ShowPopupAsync<CategoryPopupViewModel, CategoryModel?>(Shell.Current, null);

        if (result != null && result.Result != null)
        {
            result.Result.Competition = Model!;
            Categories.Add(result.Result);
        }
    }

    [RelayCommand]
    private async Task EditCategory(CategoryModel category)
    {
        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CategoryPopupViewModel.Category)] = category
        };

        // On passe une action vide pour satisfaire la signature de la méthode
        var result = await _popupService.ShowPopupAsync<CategoryPopupViewModel, CategoryModel?>(Shell.Current, options: PopupOptions.Empty, shellParameters: queryAttributes);

        if (result != null && result.Result != null)
        {
            result.Result.Competition = Model!;
            var cat = Categories.FirstOrDefault(c => c.Id == result.Result.Id);
            if (cat != null)
            {
                var index = Categories.IndexOf(cat);
                if (index >= 0)
                {
                    Categories[index] = result.Result;
                }
            }
        }
    }

    [RelayCommand]
    private void DeleteCategory(CategoryModel category)
    {
        if (category != null && Categories.Contains(category))
        {
            Categories.Remove(category);
        }
    }

    [RelayCommand]
    private async Task ShowCompetitorsCommand(CategoryModel category)
    {

    }

    [RelayCommand]
    private async Task AddCompetitor(CategoryModel category)
    {
        if (category == null) return;

        var newCompetitor = new CompetitorModel
        {
            Genre = category.Genre,
            BirthDate = DateTime.Now.AddYears(-20)
        };

        category.Competitors.Add(newCompetitor);

        var navigationParameter = new Dictionary<string, object>
        {
            { "Competitor", newCompetitor }
        };

        await Shell.Current.GoToAsync("CompetitorPage", navigationParameter);
        RefreshCategory(category);
    }

    [RelayCommand]
    private async Task EditCompetitor(CompetitorModel competitor)
    {
        if (competitor == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "Competitor", competitor }
        };

        await Shell.Current.GoToAsync("CompetitorPage", navigationParameter);
    }

    [RelayCommand]
    private void DeleteCompetitor(CompetitorModel competitor)
    {
        if (competitor != null)
        {
            foreach (var cat in Categories)
            {
                if (cat.Competitors.Contains(competitor))
                {
                    cat.Competitors.Remove(competitor);
                    RefreshCategory(cat);
                    break;
                }
            }
        }
    }
    #endregion

    #region Override Methods
    protected override void OnModelUpdated(CompetitionModel value)
    {
        if (value != null)
        {
            Categories.CollectionChanged -= OnCategoriesCollectionChanged;
            Categories = new ObservableCollection<CategoryModel>(value.Categories ?? new());
            Categories.CollectionChanged += OnCategoriesCollectionChanged;
            Validate();
        }
    }
    #endregion
}
