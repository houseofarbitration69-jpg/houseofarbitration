#region Imports
using House.Of.Arbitration.Views;
using House.Of.Arbitration.Views.Competition;
using House.Of.Arbitration.Views.Wizard.Competition;
using House.Of.Arbitration.Views.Wizard.Competition.Steps;
#endregion

namespace House.Of.Arbitration.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("HomePage", typeof(HomePage));

        Routing.RegisterRoute("MasterPage", typeof(MasterPage));
        Routing.RegisterRoute("SlavePage", typeof(SlavePage));

        Routing.RegisterRoute("CompetitionWizard", typeof(WizardPage));
        Routing.RegisterRoute("Competitions", typeof(CompetitionsPage));
        Routing.RegisterRoute("CompetitorsPage", typeof(CompetitorsPage));
        Routing.RegisterRoute("DrawPage", typeof(DrawPage));
        Routing.RegisterRoute("Test", typeof(DragDropPage));

        Routing.RegisterRoute("Server", typeof(ServerPage));
    }
}
