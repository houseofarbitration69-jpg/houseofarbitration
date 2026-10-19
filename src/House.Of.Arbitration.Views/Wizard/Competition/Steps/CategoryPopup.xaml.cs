#region Imports
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class CategoryPopup
{
    #region Constructors
    /// <summary>
    /// 
    /// </summary>
    /// <param name="viewModel"></param>
    public CategoryPopup(CategoryPopupViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
	}
    #endregion
}
