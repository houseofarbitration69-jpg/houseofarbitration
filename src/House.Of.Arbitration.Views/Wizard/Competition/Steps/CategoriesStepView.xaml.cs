using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class CategoriesStepView : ContentView
{
	public CategoriesStepView()
	{
		InitializeComponent();
        
        // Initialisation des Pickers
        TypePicker.ItemsSource = Enum.GetValues(typeof(CategoryType));
        RoundPicker.ItemsSource = Enum.GetValues(typeof(RoundType));
        GenrePicker.ItemsSource = Enum.GetValues(typeof(Genre));
        AgePicker.ItemsSource = Enum.GetValues(typeof(AgeRange));
        
        TypePicker.SelectedIndex = 1;
        RoundPicker.SelectedIndex = 1;
        GenrePicker.SelectedIndex = 1;
        AgePicker.SelectedIndex = 1;
	}

    private void OnAddCategoryClicked(object sender, EventArgs e)
    {
        PopupOverlay.IsVisible = true;
    }

    private void OnClosePopupClicked(object sender, EventArgs e)
    {
        PopupOverlay.IsVisible = false;
    }

    private void OnSaveCategoryClicked(object sender, EventArgs e)
    {
        if (BindingContext is ViewModels.Wizard.Competition.Steps.CategoriesStepViewModel vm)
        {
            var newCategory = new CategoryModel
            {
                Type = (CategoryType)TypePicker.SelectedItem,
                RoundType = (RoundType)RoundPicker.SelectedItem,
                Genre = (Genre)GenrePicker.SelectedItem,
                AgeRange = (AgeRange)AgePicker.SelectedItem,
                WeightMin = int.TryParse(MinWeightEntry.Text, out int min) ? min : 0,
                WeightMax = int.TryParse(MaxWeightEntry.Text, out int max) ? max : 100,
                //Competitors = vm.Competitors.ToList(),
                Competition = vm.Model
            };

            vm.AddCategoryCommand.Execute(newCategory);
        }

        PopupOverlay.IsVisible = false;
    }
}
