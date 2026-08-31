#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class MvtTimeCodeModel : ObservableObject
{
    public DateTime Date { get; set; }

    public MvtCodeModel? Code { get; set; }

    public double Score { get; set; } = 0.0;
}