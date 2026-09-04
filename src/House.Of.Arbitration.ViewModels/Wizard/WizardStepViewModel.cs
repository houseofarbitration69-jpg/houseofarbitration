#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.ViewModels.Core;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard;

public abstract partial class WizardStepViewModel<T> : ObservableObject where T : class
{
    #region Attributs
    private bool _isValid;
    private bool _isBusy;
    private T _model = default!;
    #endregion

    #region Services
    protected readonly IPopupService _popupService;
    #endregion

    #region Properties
    public abstract string Title { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }
   
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
    public WizardStepViewModel(ResourceProvider resourceProvider, IPopupService popupService)
    {
        Resources = resourceProvider;
        _popupService = popupService;
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

    #region Public Methods
    public virtual async Task OnAppearing()
    {
        await Task.CompletedTask;
    }

    public virtual async Task Save()
    {
    }
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
