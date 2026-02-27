namespace House.Of.Arbitration.Maui.Designer.Settings;

public partial class WizardPage : BasePage
{
    private readonly WizardViewModel _viewModel;

    public WizardPage()
    {
        InitializeComponent();
        
        _viewModel = new WizardViewModel();

        var createCompetitionStep = new CreateCompetitionStepViewModel();
        var categoriesStep = new CategoriesStepViewModel();
        var userStep = new UserStepViewModel();
        var termsStep = new TermsStepViewModel();
        var summaryStep = new SummaryStepViewModel(userStep, termsStep);

        _viewModel.AddStep(createCompetitionStep);
        _viewModel.AddStep(categoriesStep);
        _viewModel.AddStep(userStep);
        _viewModel.AddStep(termsStep);
        _viewModel.AddStep(summaryStep);
        
        _viewModel.ScrollToRequested += async (index) => 
        {
            await Task.Yield(); // Laisser le temps à MAUI de souffler
            MainThread.BeginInvokeOnMainThread(() => 
            {
                // ScrollTo avec position Center est plus stable sur Android/iOS
                WizardCarousel.ScrollTo(index, position: ScrollToPosition.Center, animate: true);
            });
        };

        BindingContext = _viewModel;
        IsMenuVisible = false;
        IsBackButtonVisible = true;
    }
}