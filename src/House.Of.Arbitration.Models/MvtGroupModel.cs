#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class MvtGroupModel : ObservableObject
{
    /// <summary>
    /// Obtient ou définit l'identifiant du type de mouvement
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Obtient ou définit le libellé du groupe de mouvement
    /// </summary>
    public string Label { get; set; } = string.Empty;

    public List<MvtCodeModel>? MvtCodes { get; set; }

    /// <summary>
    /// Liste statique des groupes de mouvements par défaut
    /// </summary>
    public static List<MvtGroupModel> DefaultGroups => new()
    {
        new MvtGroupModel { Id = 1, Label = "Groupe 1 - Sauts et Aterrissages" },
        new MvtGroupModel { Id = 2, Label = "Groupe 2 - Equilibres et Postures" },
        new MvtGroupModel { Id = 3, Label = "Groupe 3 - Techniques de Balayage et Coups" }
    };
}
