#region Imports
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Input;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class TimerPickerPopupViewModel : BaseViewModel
{
    #region Constructors
    public TimerPickerPopupViewModel(
        ILogger<TimerPickerPopupViewModel> logger,
        ResourceProvider resourceProvider,
        IPopupService popupService) : base(logger, resourceProvider, popupService)
    {
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task Select(string? value)
    {
        await _popupService.ClosePopupAsync<string>(Shell.Current, value ?? string.Empty);
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await _popupService.ClosePopupAsync<string>(Shell.Current, string.Empty);
    }
    #endregion
}
