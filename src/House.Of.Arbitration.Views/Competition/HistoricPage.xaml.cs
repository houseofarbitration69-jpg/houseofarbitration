#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views;

public partial class HistoricPage : BasePage<HistoricViewModel>
{
	public HistoricPage(HistoricViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}