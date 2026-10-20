#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

public partial class ChangeOrderPopupViewModel : ObservableObject, IQueryAttributable
{
    #region Services
    /// <summary>
    /// Service de gestion des popups
    /// </summary>
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private DrawMatchItemViewModel? _match;
    private int _totalMatches = 1;
    private int _targetOrder = 1;
    private string _targetOrderString = "1";
    #endregion

    #region Properties
    /// <summary>
    /// Obtient ou définit le match
    /// </summary>
    public DrawMatchItemViewModel? Match
    {
        get => _match;
        set => SetProperty(ref _match, value);
    }

    /// <summary>
    /// Obtient ou définit le nombre total de rencontre
    /// </summary>
    public int TotalMatches
    {
        get => _totalMatches;
        set => SetProperty(ref _totalMatches, value);
    }

    /// <summary>
    /// Obtient ou définit l'ordre cible
    /// </summary>
    public int TargetOrder
    {
        get => _targetOrder;
        set
        {
            if (SetProperty(ref _targetOrder, value))
            {
                TargetOrderString = value.ToString();
            }
        }
    }

    /// <summary>
    /// Obtient ou définit l'ordre cible en chaine
    /// </summary>
    public string TargetOrderString
    {
        get => _targetOrderString;
        set
        {
            if (SetProperty(ref _targetOrderString, value))
            {
                if (int.TryParse(value, out int parsed))
                {
                    _targetOrder = parsed;
                    OnPropertyChanged(nameof(TargetOrder));
                }
            }
        }
    }

    /// <summary>
    /// Obtient le service de traduction
    /// </summary>
    public ResourceProvider Resources { get; }
    #endregion

    #region Constructors
    /// <summary>
    /// Constructeur permttant d'initialiser les services
    /// </summary>
    /// <param name="popupService">Service de gestion des popups</param>
    /// <param name="resourceProvider">Service de traduction</param>
    public ChangeOrderPopupViewModel(
        IPopupService popupService, 
        ResourceProvider resourceProvider
    )
    {
        _popupService = popupService;
        Resources = resourceProvider;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("Match") && query["Match"] is DrawMatchItemViewModel match)
        {
            Match = match;
            TargetOrder = match.GlobalOrder;
        }

        if (query.ContainsKey("TotalMatches") && query["TotalMatches"] is int total)
        {
            TotalMatches = total;
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Commande pour incrémenter l'ordre
    /// </summary>
    [RelayCommand]
    private void IncrementOrder()
    {
        if (TargetOrder < TotalMatches)
        {
            TargetOrder++;
        }
    }

    /// <summary>
    /// Commande pour décrementer l'ordre
    /// </summary>
    [RelayCommand]
    private void DecrementOrder()
    {
        if (TargetOrder > 1)
        {
            TargetOrder--;
        }
    }

    /// <summary>
    /// Commande pour le faire passer en premier
    /// </summary>
    [RelayCommand]
    private void SetFirst()
    {
        TargetOrder = 1;
    }

    /// <summary>
    /// Commande pour le faire passer en dernier
    /// </summary>
    [RelayCommand]
    private void SetLast()
    {
        if (TotalMatches > 0)
        {
            TargetOrder = TotalMatches;
        }
    }

    /// <summary>
    /// Commande permettant de valider l'ordre
    /// </summary>
    [RelayCommand]
    private async Task Validate()
    {
        if (!string.IsNullOrWhiteSpace(TargetOrderString) && int.TryParse(TargetOrderString, out int parsed))
        {
            _targetOrder = parsed;
        }

        int finalOrder = Math.Clamp(TargetOrder, 1, Math.Max(1, TotalMatches));
        await _popupService.ClosePopupAsync<int?>(Shell.Current, finalOrder);
    }

    /// <summary>
    /// Commande permettant de fermer la popup
    /// </summary>
    [RelayCommand]
    private async Task Cancel()
    {
        await _popupService.ClosePopupAsync<int?>(Shell.Current, null);
    }
    #endregion
}
