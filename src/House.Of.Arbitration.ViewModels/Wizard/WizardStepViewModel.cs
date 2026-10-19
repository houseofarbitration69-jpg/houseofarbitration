#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard;

public abstract partial class WizardStepViewModel<T> : ObservableObject where T : class
{
    #region Attributs
    private T _model = default!;
    #endregion

    #region Services
    /// <summary>
    /// Service de gestion des popups
    /// </summary>
    protected readonly IPopupService _popupService;
    #endregion

    #region Properties
    /// <summary>
    /// Obtient le titre de la page
    /// </summary>
    public abstract string Title { get; }

    /// <summary>
    /// Obtient ou définit si la page est en cour de chargement
    /// </summary>
    [ObservableProperty]
    protected bool _isBusy;

    /// <summary>
    /// Obtient ou définit si les données de la page sont valide
    /// </summary>
    [ObservableProperty]
    protected bool _isValid;

    /// <summary>
    /// Obtient ou définit les données a afficher
    /// </summary>
    public T Model
    {
        get => _model;
        set
        {
            if(_model != value)
            {
                OnModelChanged(value);
            }

            SetProperty(ref _model, value);
        }
    }

    /// <summary>
    /// Gets the provider for localized string resources.
    /// </summary>
    public ResourceProvider Resources { get; }
    #endregion

    #region Constructors
    /// <summary>
    /// Constructeur permettant d'initialiser les différents services
    /// </summary>
    /// <param name="resourceProvider">Service de traduction</param>
    /// <param name="popupService">Service de gestion des popup</param>
    public WizardStepViewModel(
        ResourceProvider resourceProvider, 
        IPopupService popupService
    )
    {
        Resources = resourceProvider;
        _popupService = popupService;
    }
    #endregion

    #region UI Methods
    /// <summary>
    /// Méthode permettant d'afficher une alerte
    /// </summary>
    /// <param name="title">Titre de la fenêtre</param>
    /// <param name="message">Message contenu dans la fenêtre</param>
    /// <param name="cancel">Texte du bouton permettant de fermer la popup</param>
    /// <returns></returns>
    protected async Task DisplayAlert(string title, string message, string cancel)
    {
        await Shell.Current.CurrentPage.DisplayAlertAsync(title, message, cancel);
    }

    /// <summary>
    /// Méthode permettant d'afficher une fenêtre de confirmation
    /// </summary>
    /// <param name="title">Titre de la fenêtre</param>
    /// <param name="message">Contenu de la fenêtre</param>
    /// <param name="accept">Texte du bouton de validation</param>
    /// <param name="cancel">Texte du bouton d'annulation</param>
    /// <returns></returns>
    protected async Task<bool> DisplayConfirmation(string title, string message, string accept, string cancel)
    {
        var queryAttributes = new Dictionary<string, object>
        {
            { Constants.QueryParameter.TITLE, title },
            { Constants.QueryParameter.MESSAGE, message },
            { Constants.QueryParameter.ACCEPT, accept },
            { Constants.QueryParameter.CANCEL, cancel }
        };

        var result = await _popupService.ShowPopupAsync<ConfirmationPopupViewModel, bool>(Shell.Current, shellParameters: queryAttributes);
        return result.Result;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Méthode appellés lors de l'apparition de la page
    /// </summary>
    /// <returns></returns>
    public virtual async Task OnAppearing() { }

    /// <summary>
    /// Méthode appellée pour sauvegarder les données de la page
    /// </summary>
    /// <returns></returns>
    public virtual async Task Save() { }
    #endregion

    #region Protected Methods
    /// <summary>
    /// Méthode générée par CommunityToolkit.Mvvm lors du changement de Model.
    /// </summary>
    protected void OnModelChanged(T value)
    {
        OnModelUpdated(value);
    }

    /// <summary>
    /// Méthode à surcharger dans les étapes pour synchroniser les données.
    /// </summary>
    protected virtual void OnModelUpdated(T value)
    {
    }
    #endregion
}
