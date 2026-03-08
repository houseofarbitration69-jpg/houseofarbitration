using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoriesStepViewModel : WizardStepViewModel<CompetitionModel>
{
    [ObservableProperty]
    private ObservableCollection<CategoryModel> _categories = new();

    [ObservableProperty]
    private CategoryModel? _selectedCategory;

    [ObservableProperty]
    private bool _isCompetitorPopupVisible;

    [ObservableProperty]
    private ObservableCollection<CompetitorModel> _currentCompetitors = new();

    public override string Title => "Catégories";

    public CategoriesStepViewModel()
    {
        Categories.CollectionChanged += OnCategoriesCollectionChanged;
        Validate();
    }

    private void OnCategoriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Model != null)
        {
            Model.Categories = Categories.ToList();
        }
        Validate();
    }

    [RelayCommand]
    private void AddCategory(CategoryModel category)
    {
        if (category != null)
        {
            category.Competition = Model!;
            category.CompetitionId = Model?.Id ?? 0;
            Categories.Add(category);
        }
    }

    [RelayCommand]
    private void RemoveCategory(CategoryModel category)
    {
        if (category != null && Categories.Contains(category))
        {
            Categories.Remove(category);
        }
    }

    [RelayCommand]
    private void ManageCompetitors(CategoryModel category)
    {
        SelectedCategory = category;
        CurrentCompetitors = new ObservableCollection<CompetitorModel>(category.Competitors ?? new());
        IsCompetitorPopupVisible = true;
    }

    [RelayCommand]
    private void SaveCompetitors()
    {
        if (SelectedCategory != null)
        {
            SelectedCategory.Competitors = CurrentCompetitors.ToList();
        }
        IsCompetitorPopupVisible = false;
        SelectedCategory = null;
    }

    [RelayCommand]
    private void AddCompetitor(string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && SelectedCategory != null)
        {
            var competitor = new CompetitorModel 
            { 
                Name = name, 
                Genre = SelectedCategory.Genre, // Par défaut le genre de la catégorie
                CategoryId = SelectedCategory.Id
            };
            CurrentCompetitors.Add(competitor);
        }
    }

    [RelayCommand]
    private void RemoveCompetitor(CompetitorModel competitor)
    {
        if (competitor != null && CurrentCompetitors.Contains(competitor))
        {
            CurrentCompetitors.Remove(competitor);
        }
    }

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

    private void Validate()
    {
        IsValid = Categories != null && Categories.Count > 0;
    }
}
