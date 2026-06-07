using CommunityToolkit.Maui;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class CategoryPopup
{
	public CategoryPopup(CategoryPopupViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
	}

    private void OnCancelClicked(object sender, EventArgs e)
    {
        //Close(null);
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        //if (BindingContext is AddCategoryPopupViewModel vm)
        //{
        //    Close(vm.GetResult());
        //}
        //else
        //{
        //    Close(null);
        //}
    }
}
