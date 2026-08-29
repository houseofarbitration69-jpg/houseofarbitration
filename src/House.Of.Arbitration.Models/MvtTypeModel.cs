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

    /// <summary>
    /// Liste statique des types de mouvements par défaut
    /// </summary>
    public static List<MvtTypeModel> DefaultTypes => new()
    {
        new MvtTypeModel { Id = 1, Label = "Type A - Difficulté de base" },
        new MvtTypeModel { Id = 2, Label = "Type B - Difficulté intermédiaire" },
        new MvtTypeModel { Id = 3, Label = "Type C - Haute difficulté" }
    };
}
