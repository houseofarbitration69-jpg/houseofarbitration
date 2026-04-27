using CommunityToolkit.Mvvm.ComponentModel;

namespace House.Of.Arbitration.Models;

public partial class JudgeModel : ObservableObject
{
    [ObservableProperty]
    private int _redPoints;

    [ObservableProperty]
    private int _bluePoints;

    [ObservableProperty]
    private string _name = string.Empty;

    public int Number { get; set; }
}
