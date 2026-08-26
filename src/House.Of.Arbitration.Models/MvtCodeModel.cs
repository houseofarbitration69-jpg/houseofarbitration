#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class MvtCodeModel : ObservableObject
{
    public int Id { get; set; }

    public string Code { get; set; } = String.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public MvtTypeModel? Type { get; set; }

    public MvtGroupModel? Group { get; set; }
}
