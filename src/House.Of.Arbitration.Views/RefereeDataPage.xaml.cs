using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
using Microsoft.Extensions.Logging;

namespace House.Of.Arbitration.Views;

public partial class RefereeDataPage : BasePage<RefereeDataViewModel>
{
	public RefereeDataPage(RefereeDataViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}