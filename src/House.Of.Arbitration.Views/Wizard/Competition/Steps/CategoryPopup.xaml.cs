#region Imports
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using House.Of.Arbitration.Views.Core.Helpers;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class CategoryPopup
{
    #region Constructors
    /// <summary>
    /// Initialise une nouvelle instance de CategoryPopup
    /// </summary>
    /// <param name="viewModel"></param>
    public CategoryPopup(CategoryPopupViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
        this.SetNavigationBarColor();
	}
    #endregion
}
