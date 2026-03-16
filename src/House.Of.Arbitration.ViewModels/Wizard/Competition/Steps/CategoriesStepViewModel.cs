#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoriesStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Services
    private readonly IRepository<CategoryModel> _repository;
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
    public CategoriesStepViewModel(IPopupService popupService, ResourceProvider resourceProvider, IRepository<CategoryModel> repository) : base(resourceProvider, popupService)
    {
        _repository = repository;
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
            var category = result.Result;
            category.CompetitionId = Model?.Id;

            await _repository.AddAsync(category);
            Categories.Add(category);
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
            var editCategory = result.Result;
            editCategory.CompetitionId = Model?.Id;
            var cat = Categories.FirstOrDefault(c => c.Id == result.Result.Id);

            if (cat != null)
            {
                var index = Categories.IndexOf(cat);
                if (index >= 0)
                {
                    await _repository.UpdateAsync(editCategory);
                    Categories[index] = editCategory;                    
                }
            }
        }
    }

    [RelayCommand]
    private async Task DeleteCategory(CategoryModel category)
    {
        if (category == null) return;

        bool confirm = await DisplayConfirmation(
            Resources.CONFIRM_DELETE,
            Resources.DELETE_CATEGORY_MESSAGE,
            Resources.YES,
            Resources.NO);

        if (confirm && Categories.Contains(category))
        {
            await _repository.DeleteAsync(category);
            Categories.Remove(category);
        }
    }

    [RelayCommand]
    private async Task ShowCompetitors(CategoryModel category)
    {
        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CompetitorsPageViewModel.Category)] = category
        };

        await Shell.Current.GoToAsync("CompetitorsPage", queryAttributes);
        //RefreshCategory(category);
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
    private async Task DeleteCompetitor(CompetitorModel competitor)
    {
        if (competitor == null) return;

        bool confirm = await DisplayConfirmation(
            Resources.CONFIRM_DELETE,
            Resources.DELETE_COMPETITOR_MESSAGE,
            Resources.YES,
            Resources.NO);

        if (confirm)
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
