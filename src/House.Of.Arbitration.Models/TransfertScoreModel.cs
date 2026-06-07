#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class TransfertScoreModel : ObservableObject
{
    public int Score { get; set; } = 0;

    public int? CompetitorId { get; set; }
}
