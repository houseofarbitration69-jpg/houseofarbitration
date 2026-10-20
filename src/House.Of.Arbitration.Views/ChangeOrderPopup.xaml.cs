#region Imports
using CommunityToolkit.Maui.Views;
using House.Of.Arbitration.ViewModels.Competition;
using House.Of.Arbitration.Views.Core.Helpers;
#endregion

namespace House.Of.Arbitration.Views;

public partial class ChangeOrderPopup : Popup
{
    #region Constructors
	public ChangeOrderPopup(ChangeOrderPopupViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
        this.SetNavigationBarColor();
	}
    #endregion
}
