#region Imports
using CommunityToolkit.Maui;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    #region Attributs
    private string _title = String.Empty;
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    #endregion

    #region Constructors
    public HomeViewModel(ILogger<HomeViewModel> logger, ResourceProvider resourceProvider, IPopupService popupService) : base(logger, resourceProvider, popupService)
    {
        Title = resourceProvider.APPLICATION_NAME;
    }
    #endregion
}
