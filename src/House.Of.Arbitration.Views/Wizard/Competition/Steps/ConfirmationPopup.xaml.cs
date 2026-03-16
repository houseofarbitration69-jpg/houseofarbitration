#region Imports
using House.Of.Arbitration.ViewModels.Core;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class ConfirmationPopup
{
	public ConfirmationPopup(ConfirmationPopupViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
	}
}
