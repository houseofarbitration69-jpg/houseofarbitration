#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class JudgePointsPopupViewModel : ObservableObject, IQueryAttributable
{
    #region Services
    private readonly IPopupService _popupService;
    #endregion

    #region Attributs
    private JudgeModel _judge = new();
    #endregion

    #region Properties
    /// <summary>
    /// 
    /// </summary>
    public JudgeModel Judge
    {
        get => _judge;
        set => SetProperty(ref _judge, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public ResourceProvider Resources { get; }
    #endregion

    #region Constructors
    public JudgePointsPopupViewModel(IPopupService popupService, ResourceProvider resourceProvider)
    {
        _popupService = popupService;
        Resources = resourceProvider;
    }
    #endregion

    #region Public Methods
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("Judge") && query["Judge"] is JudgeModel judge)
        {
            // Clone to avoid immediate updates if cancelled
            Judge = new JudgeModel
            {
                Name = judge.Name,
                Number = judge.Number,
                RedPoints = judge.RedPoints,
                BluePoints = judge.BluePoints
            };
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private void AddRedPoint() => Judge.RedPoints++;

    [RelayCommand]
    private void RemoveRedPoint() { if (Judge.RedPoints > 0) Judge.RedPoints--; }

    [RelayCommand]
    private void AddBluePoint() => Judge.BluePoints++;

    [RelayCommand]
    private void RemoveBluePoint() { if (Judge.BluePoints > 0) Judge.BluePoints--; }

    [RelayCommand]
    private async Task Validate()
    {
        await _popupService.ClosePopupAsync(Shell.Current, Judge);
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await _popupService.ClosePopupAsync(Shell.Current);
    }
    #endregion
}
