using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;

namespace House.Of.Arbitration.Views;

public partial class ServerSetupPopup : BaseView<ServerSetupPopupViewModel>
{
	public ServerSetupPopup(ServerSetupPopupViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}
