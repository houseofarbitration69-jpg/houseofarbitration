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
    private async Task AddCompetitor(CategoryModel category)
    {
        if (category == null) return;

        var newCompetitor = new CompetitorModel 
        { 
            Genre = category.Genre,
            BirthDate = DateTime.Now.AddYears(-20)
        };
        
        // On attache uniquement l'objet à la liste de navigation.
        // EF Core gérera le CategoryId lors de la sauvegarde.
        category.Competitors.Add(newCompetitor);

        var navigationParameter = new Dictionary<string, object>
        {
            { "Competitor", newCompetitor }
        };

        // Navigation MVVM pure via Shell
        await Shell.Current.GoToAsync("CompetitorPage", navigationParameter);
        
        // On rafraîchit l'UI au retour (si nécessaire)
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
    private void RemoveCompetitor(CompetitorModel competitor)
    {
        if (competitor != null)
        {
            // On cherche la catégorie parente pour supprimer proprement
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

    private void RefreshCategory(CategoryModel category)
    {
        var index = Categories.IndexOf(category);
        if (index != -1)
        {
            // Forcer le rafraîchissement visuel du BindableLayout
            Categories[index] = null!;
            Categories[index] = category;
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
