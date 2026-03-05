using CommunityToolkit.Mvvm.ComponentModel;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.ViewModels.Wizard;

public abstract partial class WizardStepViewModel<T> : ObservableObject where T : class
{
    #region Attributs
    private bool _isValid;
    private T _model = default!;
    #endregion

    #region Properties
    /// <summary>
    /// 
    /// </summary>
    public abstract string Title { get; }

    /// <summary>
    /// 
    /// </summary>
    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    public T Model
    {
        get => _model; 
        set => SetProperty(ref _model, value);
    }
    #endregion

    /// <summary>
    /// Méthode appelée lorsque le modèle est assigné ou modifié.
    /// Peut être surchargée par les classes dérivées.
    /// </summary>
    /// <param name="value"></param>
    protected void OnModelChanged(CompetitionModel value)
    {
        OnModelUpdated(value);
    }

    protected virtual void OnModelUpdated(CompetitionModel value)
    {
    }
}
