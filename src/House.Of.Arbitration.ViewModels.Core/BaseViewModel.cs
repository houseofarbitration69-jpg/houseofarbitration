#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;
#endregion

namespace House.Of.Arbitration.ViewModels.Core;

/// <summary>
/// A base class for all view models, providing common functionality.
/// It includes support for property change notifications and localization.
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    #region Service
    protected readonly ILogger _logger;
    protected readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private bool _isBusy = false;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the provider for localized string resources.
    /// </summary>
    public ResourceProvider Resources { get; }

    /// <summary>
    /// Get or set if viewmodel is busy
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseViewModel"/> class.
    /// </summary>
    /// <param name="resourceProvider">The localization resource provider, injected by DI.</param>
    public BaseViewModel(ILogger logger, ResourceProvider resourceProvider, IPopupService popupService)
    {
        _logger = logger;
        _popupService = popupService;

        Resources = resourceProvider;
    }
    #endregion

    #region Events
    /// <summary>
    /// Sets the application's current language and culture.
    /// </summary>
    /// <param name="cultureName">The name of the culture to set (e.g., "en-US", "fr-FR").</param>
    [RelayCommand]
    private void SetLanguage(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        LocalizationResourceManager.Instance.SetCulture(culture);
    }
    #endregion

    #region Protected Methods
    /// <summary>
    /// Check if application have permission 
    /// </summary>
    /// <typeparam name="TPermission">type of permission</typeparam>
    /// <returns>Status of permission</returns>
    protected async Task<PermissionStatus> CheckPermission<TPermission>() where TPermission : Permissions.BasePermission, new()
    {
        var permission = await Permissions.CheckStatusAsync<TPermission>();
        return permission;
    }

    /// <summary>
    /// Ask user permission
    /// </summary>
    /// <typeparam name="TPermission">type of permission</typeparam>
    /// <returns></returns>
    protected async Task<PermissionStatus> RequestPermission<TPermission>() where TPermission : Permissions.BasePermission, new()
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            TPermission permission = new TPermission();
            var status = await permission.CheckStatusAsync();
            if (status != PermissionStatus.Granted)
            {
                status = await permission.RequestAsync();
            }

            return status;
        });
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task NavigateTo(string page)
    {
        await Shell.Current.GoToAsync($"/{page}");
    }
    #endregion

    #region UI Methods
    protected async Task DisplayAlert(string title, string message, string cancel)
    {
        await Shell.Current.CurrentPage.DisplayAlertAsync(title, message, cancel);
    }

    protected async Task<bool> DisplayConfirmation(string title, string message, string accept, string cancel)
    {
        var queryAttributes = new Dictionary<string, object>
        {
            { "Title", title },
            { "Message", message },
            { "Accept", accept },
            { "Cancel", cancel }
        };

        var result = await _popupService.ShowPopupAsync<ConfirmationPopupViewModel, bool>(Shell.Current, shellParameters: queryAttributes);
        return result.Result;
    }
    #endregion

    #region Virtual Methods
    /// <summary>
    /// A virtual method that is intended to be called when the associated view is appearing.
    /// </summary>
    public virtual async Task OnAppearing() => await Task.CompletedTask;

    /// <summary>
    /// A virtual method that is intended to be called when the associated view is disappearing.
    /// </summary>
    public virtual async Task OnDisappearing() => await Task.CompletedTask;
    #endregion
}
