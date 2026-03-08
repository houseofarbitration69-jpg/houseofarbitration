#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.Views;

public partial class MasterPage : BasePage<MasterViewModel>
{
	public MasterPage(MasterViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}
