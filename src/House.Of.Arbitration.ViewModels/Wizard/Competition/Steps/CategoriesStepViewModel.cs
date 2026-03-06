using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Models;
using System.Collections.ObjectModel;

namespace House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

public partial class CategoriesStepViewModel : WizardStepViewModel<CompetitionModel>
{
    [ObservableProperty]
    private ObservableCollection<CategoryModel> _categories = new();

    public override string Title => "Catégories";

    public CategoriesStepViewModel()
    {
        Validate();
    }

    [RelayCommand]
    private void AddCategory(CategoryModel category)
    {
        if (category != null)
        {
            category.Competition = Model;
            category.CompetitionId = Model?.Id ?? -1;
            Categories.Add(category);

            if (Model != null)
            {
                Model.Categories = Categories.ToList();
            }

            Validate();
        }
    }

    [RelayCommand]
    private void RemoveCategory(CategoryModel category)
    {
        if (category != null && Categories.Contains(category))
        {
            Categories.Remove(category);

            if (Model != null)
            {
                Model.Categories = Categories.ToList();
            }

            Validate();
        }
    }

    protected override void OnModelUpdated(CompetitionModel value)
    {
        if (value != null)
        {
            // Note: CompetitionModel need to have a property List<CategoryModel> Categories
            // For now, we initialize from the model if it exists
            Categories = new ObservableCollection<CategoryModel>(value.Categories ?? new());

            Validate();
        }
    }

    private void Validate()
    {
        IsValid = Categories != null && Categories.Count > 0;
    }
}
