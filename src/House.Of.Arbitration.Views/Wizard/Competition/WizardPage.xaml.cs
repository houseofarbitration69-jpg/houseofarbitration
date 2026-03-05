#region Imports
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Wizard.Competition;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition;

public partial class WizardPage : BasePage<CompetitionWizardViewModel>
{
    public WizardPage(CompetitionWizardViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
        
        var settingsStep = new SettingsStepViewModel();
        var categoriesStep = new CategoriesStepViewModel();
        //var userStep = new UserStepViewModel();
        //var termsStep = new TermsStepViewModel();
        //var summaryStep = new SummaryStepViewModel(userStep, termsStep);

        viewModel.AddStep(settingsStep);
        viewModel.AddStep(categoriesStep);
        //_viewModel.AddStep(userStep);
        //_viewModel.AddStep(termsStep);
        //_viewModel.AddStep(summaryStep);
        
        viewModel.ScrollToRequested += async (index) => 
        {
            await Task.Yield(); // Laisser le temps à MAUI de souffler
            MainThread.BeginInvokeOnMainThread(() => 
            {
                // ScrollTo avec position Center est plus stable sur Android/iOS
                WizardCarousel.ScrollTo(index, position: ScrollToPosition.Center, animate: true);
            });
        };

        //IsMenuVisible = false;
        //IsBackButtonVisible = true;
    }
}
