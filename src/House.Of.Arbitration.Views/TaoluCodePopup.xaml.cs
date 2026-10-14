#region Imports
using CommunityToolkit.Maui.Views;
using House.Of.Arbitration.ViewModels;
#endregion

namespace House.Of.Arbitration.Views;

public partial class TaoluCodePopup : Popup
{
    public TaoluCodePopup(TaoluCodePopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
