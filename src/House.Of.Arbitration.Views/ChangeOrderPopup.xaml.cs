using CommunityToolkit.Maui.Views;
using House.Of.Arbitration.ViewModels.Competition;

namespace House.Of.Arbitration.Views;

public partial class ChangeOrderPopup : Popup
{
	public ChangeOrderPopup(ChangeOrderPopupViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
