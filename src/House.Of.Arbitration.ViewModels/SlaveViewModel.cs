#region Imports
using House.Of.Arbitration.Data.Abstractions;
using House.Of.Arbitration.Localization;
using House.Of.Arbitration.Models;
using House.Of.Arbitration.ViewModels.Core;
using Microsoft.Extensions.Logging;
#endregion

namespace House.Of.Arbitration.ViewModels;

public partial class SlaveViewModel : BaseViewModel
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
    public SlaveViewModel(ILogger<SlaveViewModel> logger, ResourceProvider resourceProvider, IRepository<CompetitionModel> repository) : base(logger, resourceProvider)
    {
        Title = resourceProvider.APPLICATION_NAME;
    }
    #endregion
}
