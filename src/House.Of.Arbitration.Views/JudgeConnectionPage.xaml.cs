#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views;

public partial class JudgeConnectionPage : BasePage<JudgeConnectionViewModel>
{
	public JudgeConnectionPage(JudgeConnectionViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}
}