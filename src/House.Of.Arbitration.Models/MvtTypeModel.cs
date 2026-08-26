#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class MvtTypeModel : ObservableObject
{
    /// <summary>
    /// Obtient ou définit l'identifiant du type de mouvement
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Obtient ou définit le libellé du groupe de mouvement
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
