#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class MvtTimeCodeModel : ObservableObject
{
    private DateTime _date;
    private MvtCodeModel? _code;
    private double _score = 0.0;
    private bool _canDelete;

    public DateTime Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    public MvtCodeModel? Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public double Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    public bool CanDelete
    {
        get => _canDelete;
        set => SetProperty(ref _canDelete, value);
    }
}