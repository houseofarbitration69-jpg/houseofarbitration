#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
using House.Of.Arbitration.Controls;
#endregion

namespace House.Of.Arbitration.Views;

public partial class ServerPage : BasePage<ServerViewModel>
{
	public ServerPage(ServerViewModel viewModel) : base(viewModel)
	{
	InitializeComponent();

	    // Dummy references to ensure custom actions are not stripped in Release mode
	    _ = typeof(House.Of.Arbitration.Controls.FadeAction);
	    _ = typeof(House.Of.Arbitration.Controls.TranslateAction);
	}
    private bool _isSideSheetOpen;

    //private async void OnMenuClicked(object sender, EventArgs e)
    //{
    //    if (_isSideSheetOpen)
    //        await CloseSideSheet();
    //    else
    //        await OpenSideSheet();
    //}

    //private async void OnOverlayTapped(object sender, EventArgs e)
    //{
    //    await CloseSideSheet();
    //}

    //private async Task OpenSideSheet()
    //{
    //    _isSideSheetOpen = true;
    //    Overlay.IsVisible = true;
    //    SideSheet.IsVisible = true;
    //    await Task.WhenAll(
    //        SideSheet.TranslateTo(0, 0, 300, Easing.CubicOut),
    //        Overlay.FadeTo(0.5, 300)
    //    );
    //}

    //private async Task CloseSideSheet()
    //{
    //    _isSideSheetOpen = false;
    //    await Task.WhenAll(
    //        SideSheet.TranslateTo(-300, 0, 300, Easing.CubicIn),
    //        Overlay.FadeTo(0, 300)
    //    );
    //    Overlay.IsVisible = false;
    //    SideSheet.IsVisible = false;
    //}
}
