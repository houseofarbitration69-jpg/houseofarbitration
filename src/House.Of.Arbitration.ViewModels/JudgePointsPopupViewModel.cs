using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.ViewModels;

public partial class JudgePointsPopupViewModel : ObservableObject, IQueryAttributable
{
    private readonly IPopupService _popupService;
    
    [ObservableProperty]
    private JudgeModel _judge = new();

    public ResourceProvider Resources { get; }

    public JudgePointsPopupViewModel(IPopupService popupService, ResourceProvider resourceProvider)
    {
        _popupService = popupService;
        Resources = resourceProvider;
    }

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
}
