#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoriesStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Services
    private readonly IRepository<CategoryModel> _repository;
    private readonly IRepository<DrawModel> _drawsRepository;
    private readonly IWarningService _warningService;
    #endregion

    #region Attributs
    private ObservableCollection<CategoryModel> _categories = new();
    #endregion

    #region Properties
    public override string Title => Resources.CATEGORIES;

    public ObservableCollection<CategoryModel> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }
    #endregion

    #region Constructors
    public CategoriesStepViewModel(
        IPopupService popupService, 
        ResourceProvider resourceProvider, 
        IRepository<CategoryModel> repository,
        IRepository<DrawModel> drawRepository,
        IWarningService warningService
    ) : base(resourceProvider, popupService)
    {
        _repository = repository;
        _drawsRepository = drawRepository;
        _warningService = warningService;

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

    private async Task DeleteDrawAsync(int categoryId)
    {
        var draws = await _drawsRepository.GetAllAsync();
        var existingDraw = draws?.FirstOrDefault(d => d.CategoryId == categoryId);
        if (existingDraw != null)
        {
            await _drawsRepository.DeleteAsync(existingDraw);
        }
    }

    private void Validate()
    {
        bool allValid = Categories != null && Categories.Count > 0;

        if (Categories != null)
        {
            foreach (var category in Categories)
            {
                if (category == null) continue;

                // Un tirage est considéré comme existant si l'objet Draw existe
                category.HasDraw = category.Draw != null;

                // On met à jour l'indicateur global de validité
                allValid = allValid && category.HasDraw;
            }
        }
        else
        {
            allValid = false;
        }

        IsValid = allValid;
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// Refresh categories when appearing to catch updates (like new competitors or draws)
    /// </summary>
    public override async Task OnAppearing()
    {
        IsBusy = true;
        try
        {
            if (Model != null)
            {
                // Reload categories with their competitors, warnings and draws to update UI
                var categories = await _repository.GetAllAsync("AgeRange", "Competitors.Competitor", "Competitors.Warnings", "Draw.DrawKnockouts", "Draw.DrawPools", "Draw.DrawOrders");
                var allDraws = await _drawsRepository.GetAllAsync("DrawKnockouts", "DrawPools", "DrawOrders");

                if (categories != null)
                {
                    var competitionCategories = categories.Where(c => c.CompetitionId == Model.Id).ToList();

                    // On pré-calcule HasDraw avant l'affichage pour éviter les flashs ou retards de notification
                    foreach (var category in competitionCategories)
                    {
                        // Fallback manuel si l'Include d'EF Core n'a pas fonctionné (conflits de schéma ou tracking)
                        if (category.Draw == null && allDraws != null)
                        {
                            category.Draw = allDraws.FirstOrDefault(d => d.CategoryId == category.Id);
                        }

                        category.HasDraw = category.Draw != null;
                    }

                    Categories.CollectionChanged -= OnCategoriesCollectionChanged;
                    Categories = new ObservableCollection<CategoryModel>(competitionCategories);
                    Categories.CollectionChanged += OnCategoriesCollectionChanged;

                    Model.Categories = competitionCategories;
                }
            }
            Validate();
        }
        finally
        {
            IsBusy = false;
        }
    }    
    #endregion

    #region Commands
    [RelayCommand]
    private async Task AddCategory()
    {
        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CategoryPopupViewModel.CompetitionType)] = Model?.Type ?? CompetitionType.None
        };

        var result = await _popupService.ShowPopupAsync<CategoryPopupViewModel, CategoryModel?>(Shell.Current, options: PopupOptions.Empty, shellParameters: queryAttributes);

        if (result != null && result.Result != null)
        {
            var category = result.Result;
            category.AgeRange = null;
            category.CompetitionId = Model?.Id;

            await _repository.AddAsync(category);
            Categories.Add(category);
            Validate();
        }
    }

    [RelayCommand]
    private async Task EditCategory(CategoryModel category)
    {
        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(CategoryPopupViewModel.Category)] = category,
            [nameof(CategoryPopupViewModel.CompetitionType)] = Model?.Type ?? category.Competition?.Type ?? CompetitionType.None
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
                    await _warningService.UpdateWarningsForCategoryAsync(editCategory.Id);
                    await DeleteDrawAsync(editCategory.Id);
                    
                    // Reload to get updated warnings in the object graph
                    await OnAppearing();
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

        if (confirm)
        {
            var searchCategory = Categories.FirstOrDefault(c => c.Id == category.Id);

            if( searchCategory != null)
            {
                await _repository.DeleteAsync(category);
                Categories.Remove(searchCategory);
            }
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
    private async Task ShowDraw(CategoryModel category)
    {
        var queryAttributes = new Dictionary<string, object>
        {
            [nameof(DrawPageViewModel.Category)] = category
        };

        await Shell.Current.GoToAsync("DrawPage", queryAttributes);
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
