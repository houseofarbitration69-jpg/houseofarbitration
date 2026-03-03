#region Imports
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public class HomeViewModel : BaseViewModel
{
    #region Attributs
    private string _title = "House Of Arbitration";
    #endregion

    #region Properties
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    #endregion

    public HomeViewModel(ILogger<HomeViewModel> logger, ResourceProvider resourceProvider) : base(logger, resourceProvider)
    {

    }
}
