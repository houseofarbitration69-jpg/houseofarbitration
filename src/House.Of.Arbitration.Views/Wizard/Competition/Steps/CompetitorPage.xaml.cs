using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;

namespace House.Of.Arbitration.Views.Wizard.Competition.Steps;

public partial class CompetitorPage 
{
	public CompetitorPage(CompetitorPageViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
        
        GenrePicker.ItemsSource = Enum.GetValues(typeof(Genre));
	}
}
