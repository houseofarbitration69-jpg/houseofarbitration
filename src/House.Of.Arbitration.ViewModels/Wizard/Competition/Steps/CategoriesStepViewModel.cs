#region Imports
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoriesStepViewModel : WizardStepViewModel<CompetitionModel>
{
    #region Attributs
    private ObservableCollection<CategoryModel> _categories = new();
    private CategoryModel? _selectedCategory;
    private bool _isCompetitorPopupVisible;
    private CompetitorModel? _selectedCompetitor;
    private ObservableCollection<CompetitorModel> _competitors = new();
    #endregion

    #region Properties
    public ObservableCollection<CategoryModel> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }
    
    public CategoryModel? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }
    
    public bool IsCompetitorPopupVisible
    {
        get => _isCompetitorPopupVisible;
        set => SetProperty(ref _isCompetitorPopupVisible, value);
    }
    
    public ObservableCollection<CompetitorModel> Competitors
    {
        get => _competitors;
        set => SetProperty(ref _competitors, value);
    }

    public CompetitorModel? SelectedCompetitor
    {
        get => _selectedCompetitor;
        set => SetProperty(ref _selectedCompetitor, value);
    }

    public override string Title => "Catégories";
    #endregion

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
            Competitors = new ObservableCollection<CompetitorModel>();
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
        SelectedCompetitor = new();
        Competitors = new ObservableCollection<CompetitorModel>(category.Competitors ?? new());
        IsCompetitorPopupVisible = true;
    }

    [RelayCommand]
    private void SaveCompetitors()
    {
        if (SelectedCategory != null)
        {
            // Grâce à [ObservableProperty] sur CategoryModel.Competitors, 
            // cette réassignation déclenche la notification à l'UI MAUI.
            SelectedCategory.Competitors = Competitors.ToList();

            var category = Categories.FirstOrDefault(c => c.Id == SelectedCategory.Id);
            if (category != null)
            {
                var index = Categories.IndexOf(category);

                if (index >= 0)
                {
                    //Categories.CollectionChanged -= OnCategoriesCollectionChanged;
                    Categories.RemoveAt(index);
                    Categories.Insert(index, SelectedCategory);
                    //OnCategoriesCollectionChanged(nameof(Categories), new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    //Categories = new ObservableCollection<CategoryModel>(Categories);
                    //Categories.CollectionChanged += OnCategoriesCollectionChanged;

                    //Categories = new ObservableCollection<CategoryModel>();
                    //OnPropertyChanged(nameof(Categories));
                }
            }
        }

        IsCompetitorPopupVisible = false;
        SelectedCategory = null;
    }

    [RelayCommand]
    private void AddCompetitor()
    {
        if (SelectedCompetitor != null && !string.IsNullOrWhiteSpace(SelectedCompetitor.Name) && SelectedCategory != null)
        {
            SelectedCompetitor.Genre = SelectedCategory.Genre;
            SelectedCompetitor.CategoryId = SelectedCategory.Id;
            Competitors.Add(SelectedCompetitor);

            SelectedCompetitor = new();
        }
    }

    [RelayCommand]
    private void RemoveCompetitor(CompetitorModel competitor)
    {
        if (competitor != null && Competitors.Contains(competitor))
        {
            Competitors.Remove(competitor);
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
