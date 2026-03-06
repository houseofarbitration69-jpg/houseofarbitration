#region Imports
using House.Of.Arbitration.ViewModels.Competition;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Competition;

public partial class CompetitionsPage : BasePage<CompetitionsViewModel>
{
	public CompetitionsPage(CompetitionsViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}

    private async void OnDetailsClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Models.CompetitionModel comp)
        {
            if (BindingContext is CompetitionsViewModel vm)
            {
                vm.ShowDetailsCommand.Execute(comp);
            }
        }
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Models.CompetitionModel comp)
        {
            if (BindingContext is CompetitionsViewModel vm)
            {
                vm.StartCompetitionCommand.Execute(comp);
            }
        }
    }
}
