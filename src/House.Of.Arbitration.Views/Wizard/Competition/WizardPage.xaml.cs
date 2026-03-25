#region Imports
using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Wizard.Competition;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition;

public partial class WizardPage : BasePage<CompetitionWizardViewModel>
{
    public WizardPage(
        IPopupService popupService,
        CompetitionWizardViewModel viewModel,
        ResourceProvider resource,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository,
        IRepository<DrawModel> drawRepository
    ) : base(viewModel)
    {
        InitializeComponent();
        
        var settingsStep = new SettingsStepViewModel(resource, popupService, competitionRepository);
        var categoriesStep = new CategoriesStepViewModel(popupService, resource, categoryRepository, drawRepository);
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
