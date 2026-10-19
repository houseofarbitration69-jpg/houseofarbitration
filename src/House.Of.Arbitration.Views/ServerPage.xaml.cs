#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
using House.Of.Arbitration.Controls;
#endregion

namespace House.Of.Arbitration.Views;

public partial class ServerPage : BasePage<ServerViewModel>
{
	public ServerPage(ServerViewModel viewModel) : base(viewModel)
	{
	InitializeComponent();

	    // Dummy references to ensure custom actions are not stripped in Release mode
	    _ = typeof(House.Of.Arbitration.Controls.FadeAction);
	    _ = typeof(House.Of.Arbitration.Controls.TranslateAction);
	}
}
