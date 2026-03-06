#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views;

public partial class OldHomePage : BasePage<HomeViewModel>
{
	public OldHomePage(HomeViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}
