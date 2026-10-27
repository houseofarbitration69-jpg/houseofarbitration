#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views;

public partial class JudgeTaoluPage : BasePage<JudgeViewModel>
{
	public JudgeTaoluPage(JudgeViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}