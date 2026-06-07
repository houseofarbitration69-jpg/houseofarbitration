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
    private string _title = string.Empty;
    private string _message = string.Empty;
    private string _acceptText = string.Empty;
    private string _cancelText = string.Empty;
    #endregion

    #region Properties
    public ResourceProvider Resources { get; }

    
    /// <summary>
    /// Obtient ou définit le titre
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Obtient ou définit le message
    /// </summary>
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    /// <summary>
    /// Obtient ou définit le message d'acceptation
    /// </summary>
    public string AcceptText
    {
        get => _acceptText;
        set => SetProperty(ref _acceptText, value);
    }

    /// <summary>
    /// Obtient ou définit le message d'annulation
    /// </summary>
    public string CancelText
    {
        get => _cancelText;
        set => SetProperty(ref _cancelText, value);
    }
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
