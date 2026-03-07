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
        // On surveille les changements dans la liste pour mettre à jour le modèle
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

    protected override void OnModelUpdated(CompetitionModel value)
    {
        if (value != null)
        {
            // Détacher l'ancien événement pour éviter les fuites/doublons
            Categories.CollectionChanged -= OnCategoriesCollectionChanged;

            // Remplir la collection à partir du modèle
            Categories = new ObservableCollection<CategoryModel>(value.Categories ?? new());

            // Réattacher l'événement sur la nouvelle collection
            Categories.CollectionChanged += OnCategoriesCollectionChanged;

            Validate();
        }
    }

    private void Validate()
    {
        IsValid = Categories != null && Categories.Count > 0;
    }
}
