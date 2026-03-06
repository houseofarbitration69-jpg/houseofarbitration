#region Imports
using House.Of.Arbitration.Views;
using House.Of.Arbitration.Views.Competition;
using House.Of.Arbitration.Views.Wizard.Competition;
#endregion

namespace House.Of.Arbitration.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("HomePage", typeof(HomePage));
        Routing.RegisterRoute("CompetitionWizard", typeof(WizardPage));
        Routing.RegisterRoute("Competitions", typeof(CompetitionsPage));
    }
}
