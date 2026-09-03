#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.ViewModels.Competition;

public partial class ChangeOrderPopupViewModel : ObservableObject, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private DrawMatchItemViewModel? _match;
    private int _totalMatches = 1;
    private int _targetOrder = 1;
    private string _targetOrderString = "1";
    #endregion

    #region Properties
    public DrawMatchItemViewModel? Match
    {
        get => _match;
        set => SetProperty(ref _match, value);
    }

    public int TotalMatches
    {
        get => _totalMatches;
        set => SetProperty(ref _totalMatches, value);
    }

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

    public ResourceProvider Resources { get; }
    #endregion

    #region Constructors
    public ChangeOrderPopupViewModel(IPopupService popupService, ResourceProvider resourceProvider)
    {
        _popupService = popupService;
        Resources = resourceProvider;
    }
    #endregion

    #region Public Methods
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
    [RelayCommand]
    private void IncrementOrder()
    {
        if (TargetOrder < TotalMatches)
        {
            TargetOrder++;
        }
    }

    [RelayCommand]
    private void DecrementOrder()
    {
        if (TargetOrder > 1)
        {
            TargetOrder--;
        }
    }

    [RelayCommand]
    private void SetFirst()
    {
        TargetOrder = 1;
    }

    [RelayCommand]
    private void SetLast()
    {
        if (TotalMatches > 0)
        {
            TargetOrder = TotalMatches;
        }
    }

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

    [RelayCommand]
    private async Task Cancel()
    {
        await _popupService.ClosePopupAsync<int?>(Shell.Current, null);
    }
    #endregion
}
