using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.ViewModels.Wizard;

public abstract partial class WizardStepViewModel<T> : ObservableObject where T : class
{
    #region Attributs
    private bool _isValid;

    [ObservableProperty]
    private T _model = default!;
    #endregion

    #region Properties
    public abstract string Title { get; }

    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }
    #endregion

    /// <summary>
    /// Méthode générée par CommunityToolkit.Mvvm lors du changement de Model.
    /// </summary>
    partial void OnModelChanged(T value)
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
