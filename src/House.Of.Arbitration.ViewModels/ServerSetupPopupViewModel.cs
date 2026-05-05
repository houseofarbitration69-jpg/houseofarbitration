#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class ServerSetupPopupViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _serverName = "SERVEUR_KUNGFU";

    [ObservableProperty]
    private string _serverDescription = "Ring 1";

    public ServerSetupPopupViewModel(
        ILogger<ServerSetupPopupViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService) : base(logger, resourceProvider, popupService)
    {
    }

    [RelayCommand]
    private async Task Confirm()
    {
        await _popupService.ClosePopupAsync<ServerSetupResult>(Shell.Current, new ServerSetupResult { Name = ServerName, Description = ServerDescription });
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }
}

public class ServerSetupResult
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
