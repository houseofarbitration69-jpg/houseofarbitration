#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
#endregion

namespace House.Of.Arbitration.ViewModels.Core;

public partial class ConfirmationPopupViewModel : ObservableObject, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _acceptText = string.Empty;

    [ObservableProperty]
    private string _cancelText = string.Empty;
    #endregion

    #region Properties
    public ResourceProvider Resources { get; }
    #endregion

    #region Constructor
    public ConfirmationPopupViewModel(IPopupService popupService, ResourceProvider resourceProvider)
    {
        _popupService = popupService;
        Resources = resourceProvider;
    }
    #endregion

    #region Implement IQueryAttributable
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("Title")) Title = query["Title"].ToString() ?? string.Empty;
        if (query.ContainsKey("Message")) Message = query["Message"].ToString() ?? string.Empty;
        if (query.ContainsKey("Accept")) AcceptText = query["Accept"].ToString() ?? string.Empty;
        if (query.ContainsKey("Cancel")) CancelText = query["Cancel"].ToString() ?? string.Empty;
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Accept()
    {
        await _popupService.ClosePopupAsync(Shell.Current, true);
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await _popupService.ClosePopupAsync(Shell.Current, false);
    }
    #endregion
}
