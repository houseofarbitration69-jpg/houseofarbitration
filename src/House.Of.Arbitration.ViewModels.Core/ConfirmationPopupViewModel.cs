#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.ViewModels.Core;

public partial class ConfirmationPopupViewModel : ObservableObject, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Properties
    /// <summary>
    /// Obtient les resources pour les trzaductions
    /// </summary>
    public ResourceProvider Resources { get; }

    /// <summary>
    /// Obtient ou définit le titre
    /// </summary>
    [ObservableProperty]
    public string _title = String.Empty;

    /// <summary>
    /// Obtient ou définit le message
    /// </summary>
    [ObservableProperty]
    public string _message = String.Empty;

    /// <summary>
    /// Obtient ou définit le message d'acceptation
    /// </summary>
    [ObservableProperty]
    public string _acceptText = String.Empty;

    /// <summary>
    /// Obtient ou définit le message d'annulation
    /// </summary>
    [ObservableProperty]
    public string _cancelText = String.Empty;
    #endregion

    #region Constructor
    /// <summary>
    /// Constructeur permettant d'initialiser les différents services
    /// </summary>
    /// <param name="popupService">Service de gestion des popups</param>
    /// <param name="resourceProvider">Service de traduction</param>
    public ConfirmationPopupViewModel(
        IPopupService popupService, 
        ResourceProvider resourceProvider
    )
    {
        _popupService = popupService;
        Resources = resourceProvider;
    }
    #endregion

    #region Implement IQueryAttributable
    /// <summary>
    /// Méthode permettant de récupérer la liste des paramètres
    /// </summary>
    /// <param name="query"></param>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey(Constants.QueryParameter.TITLE)) Title = query[Constants.QueryParameter.TITLE].ToString() ?? string.Empty;
        if (query.ContainsKey(Constants.QueryParameter.MESSAGE)) Message = query[Constants.QueryParameter.MESSAGE].ToString() ?? string.Empty;
        if (query.ContainsKey(Constants.QueryParameter.ACCEPT)) AcceptText = query[Constants.QueryParameter.ACCEPT].ToString() ?? string.Empty;
        if (query.ContainsKey(Constants.QueryParameter.CANCEL)) CancelText = query[Constants.QueryParameter.CANCEL].ToString() ?? string.Empty;
    }
    #endregion

    #region Commands
    /// <summary>
    /// Commande permettant de valider la fenêtre
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task Accept()
    {
        await _popupService.ClosePopupAsync(Shell.Current, true);
    }

    /// <summary>
    /// Méthode permettant d'annuler la fenêtre
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task Cancel()
    {
        await _popupService.ClosePopupAsync(Shell.Current, false);
    }
    #endregion
}
