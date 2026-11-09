#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class TransfertScoreModel : ObservableObject
{
    public double Score { get; set; } = 0.0;

    public int? CompetitorId { get; set; }

    public Constants.Side? Side { get; set; }

    public MvtTimeCodeModel? Code { get; set; }

    /// <summary>
    /// Code de position du juge (ex: "1", "2", "A_1") pour identifier précisément le juge en multi-instances sur un même appareil.
    /// </summary>
    public string? JudgePosition { get; set; }
}
