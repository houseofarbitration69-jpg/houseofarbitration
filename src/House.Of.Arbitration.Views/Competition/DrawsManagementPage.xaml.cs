#region Imports
using House.Of.Arbitration.ViewModels.Competition;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Competition;

public partial class DrawsManagementPage : BasePage<DrawsManagementViewModel>
{
	public DrawsManagementPage(DrawsManagementViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}
