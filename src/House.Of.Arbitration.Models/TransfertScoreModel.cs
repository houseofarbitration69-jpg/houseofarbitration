#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class TransfertScoreModel : ObservableObject
{
    public double Score { get; set; } = 0.0;

    public int? CompetitorId { get; set; }

    public MvtTimeCodeModel? Code { get; set; }
}
