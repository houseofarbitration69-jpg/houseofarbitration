#region Imports
using House.Of.Arbitration.ViewModels;
using House.Of.Arbitration.Views.Core;
#endregion

namespace House.Of.Arbitration.Views;

public partial class ServerPage : BasePage<ServerViewModel>
{
	public ServerPage(ServerViewModel viewModel) : base(viewModel)
	{
		InitializeComponent();
	}

    private bool _isSideSheetOpen;

    private async void OnMenuClicked(object sender, EventArgs e)
    {
        if (_isSideSheetOpen)
            await CloseSideSheet();
        else
            await OpenSideSheet();
    }

    private async void OnOverlayTapped(object sender, EventArgs e)
    {
        await CloseSideSheet();
    }

    private async Task OpenSideSheet()
    {
        _isSideSheetOpen = true;
        Overlay.IsVisible = true;
        SideSheet.IsVisible = true;
        await Task.WhenAll(
            SideSheet.TranslateTo(0, 0, 300, Easing.CubicOut),
            Overlay.FadeTo(0.5, 300)
        );
    }

    private async Task CloseSideSheet()
    {
        _isSideSheetOpen = false;
        await Task.WhenAll(
            SideSheet.TranslateTo(-300, 0, 300, Easing.CubicIn),
            Overlay.FadeTo(0, 300)
        );
        Overlay.IsVisible = false;
        SideSheet.IsVisible = false;
    }
}
