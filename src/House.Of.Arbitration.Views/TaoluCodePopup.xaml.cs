#region Imports
using CommunityToolkit.Maui.Views;
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core.Helpers;
#endregion

namespace House.Of.Arbitration.Views;

public partial class TaoluCodePopup : Popup
{
    #region Constructors
    public TaoluCodePopup(TaoluCodePopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.SetNavigationBarColor();
    }
    #endregion
}
