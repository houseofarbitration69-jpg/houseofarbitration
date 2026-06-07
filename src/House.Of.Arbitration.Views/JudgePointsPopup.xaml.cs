using CommunityToolkit.Maui.Views;
using House.Of.Arbitration.ViewModels;

namespace House.Of.Arbitration.Views;

public partial class JudgePointsPopup : Popup
{
	public JudgePointsPopup(JudgePointsPopupViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
