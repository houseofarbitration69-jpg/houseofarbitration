namespace House.Of.Arbitration.Maui.Designer;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("SettingsPage", typeof(Settings.WizardPage));
        Routing.RegisterRoute("CompetitionsPage", typeof(CompetitionsPage));
    }
}
