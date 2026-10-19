#region Imports
using House.Of.Arbitration.Views;
using House.Of.Arbitration.Views.Competition;
using House.Of.Arbitration.Views.Wizard.Competition;
using House.Of.Arbitration.Views.Wizard.Competition.Steps;
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(Constants.Navigation.Page.HOME, typeof(HomePage));

        Routing.RegisterRoute(Constants.Navigation.Page.MASTER, typeof(MasterPage));
        Routing.RegisterRoute(Constants.Navigation.Page.JUDGE, typeof(JudgePage));

        Routing.RegisterRoute(Constants.Navigation.Page.COMPETITION_WIZARD, typeof(WizardPage));
        Routing.RegisterRoute(Constants.Navigation.Page.COMPETITIONS, typeof(CompetitionsPage));
        Routing.RegisterRoute(Constants.Navigation.Page.DRAWS_MANAGEMENT, typeof(DrawsManagementPage));
        Routing.RegisterRoute(Constants.Navigation.Page.COMPETITORS, typeof(CompetitorsPage));
        Routing.RegisterRoute(Constants.Navigation.Page.DRAWS, typeof(DrawPage));

        Routing.RegisterRoute(Constants.Navigation.Page.SERVER_SANDA, typeof(ServerPage));
        Routing.RegisterRoute(Constants.Navigation.Page.SERVER_TAOLU, typeof(ServerTaoluPage));
        Routing.RegisterRoute(Constants.Navigation.Page.REFEREE_DATA, typeof(RefereeDataPage));
        Routing.RegisterRoute(Constants.Navigation.Page.TAOLU_CODES, typeof(TaoluCodesPage));
    }
}
