#region Importd
using House.Of.Arbitration.Views;
#endregion

namespace House.Of.Arbitration.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("HomePage", typeof(HomePage));
    }
}
