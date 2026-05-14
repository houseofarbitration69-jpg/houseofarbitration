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
    #region Attributs
    private string _serverName = "SERVEUR_KUNGFU";
    private string _serverDescription = "Ring 1";
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit le nom du serveur
    /// </summary>
    public string ServerName
    { 
        get => _serverName; 
        set => SetProperty(ref _serverName, value); 
    }

    /// <summary>
    /// Obtient ou définit la description du serveur
    /// </summary>
    public string ServerDescription
    {
        get => _serverDescription;
        set => SetProperty(ref _serverDescription, value);
    }
    #endregion

    #region Constructors
    public ServerSetupPopupViewModel(
        ILogger<ServerSetupPopupViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService) : base(logger, resourceProvider, popupService)
    {
    }
    #endregion

    #region Commands
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
    #endregion
}

public class ServerSetupResult
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
