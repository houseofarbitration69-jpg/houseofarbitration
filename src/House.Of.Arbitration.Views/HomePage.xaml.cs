#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views;

public partial class HomePage : BasePage<HomeViewModel>
{
    public HomePage(HomeViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}