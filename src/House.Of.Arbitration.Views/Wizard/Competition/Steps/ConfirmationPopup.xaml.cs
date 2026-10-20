#region Imports
using House.Of.Arbitration.ViewModels.Core;
using House.Of.Arbitration.Views.Core.Helpers;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class ConfirmationPopup
{
    #region Constructors
	public ConfirmationPopup(ConfirmationPopupViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
        this.SetNavigationBarColor();
	}
    #endregion
}
