#region Imports
using CommunityToolkit.Maui;
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.Services.Abstractions;
using House.Of.Arbitration.ViewModels.Wizard.Competition;
using House.Of.Arbitration.ViewModels.Wizard.Competition.Steps;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views.Wizard.Competition;

public partial class WizardPage : BasePage<CompetitionWizardViewModel>
{
    #region Constructors
    /// <summary>
    /// 
    /// </summary>
    /// <param name="popupService"></param>
    /// <param name="viewModel"></param>
    /// <param name="resource"></param>
    /// <param name="warningService"></param>
    /// <param name="competitionRepository"></param>
    /// <param name="categoryRepository"></param>
    /// <param name="drawRepository"></param>
    public WizardPage(
        IPopupService popupService,
        CompetitionWizardViewModel viewModel,
        ResourceProvider resource,
        IWarningService warningService,
        IRepository<CompetitionModel> competitionRepository,
        IRepository<CategoryModel> categoryRepository,
        IRepository<DrawModel> drawRepository
    ) : base(viewModel)
    {
        InitializeComponent();
        
        var settingsStep = new SettingsStepViewModel(resource, popupService, competitionRepository);
        var categoriesStep = new CategoriesStepViewModel(popupService, resource, categoryRepository, drawRepository, warningService);
        viewModel.AddStep(settingsStep);
        viewModel.AddStep(categoriesStep);
        
        viewModel.ScrollToRequested += async (index) => 
        {
            await Task.Yield(); // Laisser le temps à MAUI de souffler
            MainThread.BeginInvokeOnMainThread(() => 
            {
                // ScrollTo avec position Center est plus stable sur Android/iOS
                WizardCarousel.ScrollTo(index, position: ScrollToPosition.Center, animate: true);
            });
        };
    }
    #endregion
}
