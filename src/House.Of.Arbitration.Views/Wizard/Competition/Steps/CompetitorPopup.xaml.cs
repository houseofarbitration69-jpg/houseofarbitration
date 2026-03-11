using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class CompetitorPopup
{
	public CompetitorPopup(CompetitorPopupViewModel viewModel)
	{
		InitializeComponent();
        BindingContext = viewModel;
	}
}
