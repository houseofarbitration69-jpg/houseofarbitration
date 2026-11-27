using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
using Microsoft.Extensions.Logging;

namespace House.Of.Arbitration.Views;

public partial class ServerHistoricPage : BasePage<ServerHistoricViewModel>
{
	public ServerHistoricPage(ServerHistoricViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}