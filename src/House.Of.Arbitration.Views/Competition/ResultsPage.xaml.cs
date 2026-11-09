#region Imports
using House.Of.Arbitration.ViewModels.Competition;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Competition;

public partial class ResultsPage : BasePage<ResultsViewModel>
{
	public ResultsPage(ResultsViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}
