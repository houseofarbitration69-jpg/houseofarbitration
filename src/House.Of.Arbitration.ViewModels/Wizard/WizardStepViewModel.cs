#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.Localization;
#endregion

namespace House.Of.Arbitration.ViewModels.Wizard;

public abstract partial class WizardStepViewModel<T> : ObservableObject where T : class
{
    #region Attributs
    private bool _isValid;
    private T _model = default!;
    #endregion

    #region Properties
    public abstract string Title { get; }

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
    public WizardStepViewModel(ResourceProvider resourceProvider)
    {
        Resources = resourceProvider;
    }
    #endregion

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
}
