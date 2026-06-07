#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class DrawModel : ObservableObject
{
    /// <summary>
    /// Obtient ou définit l'identifiant du tirage
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'identifiant de la catégorie
    /// </summary>
    public required int CategoryId { get; set; }

    /// <summary>
    /// Obtient ou définit la catégorie
    /// </summary>
    public CategoryModel? Category { get; set; }

    /// <summary>
    /// Obtient ou définit la liste des tirages en mode 'Knockout'
    /// </summary>
    public List<DrawKnockoutModel>? DrawKnockouts { get; set; }

    /// <summary>
    /// Obtient ou définit la liste des tirages en mode 'Pools'
    /// </summary>
    public List<DrawPoolsModel>? DrawPools { get; set; }
    
    /// <summary>
    /// Obtient ou définit la liste des tirages en mode 'Order'
    /// </summary>
    public List<DrawOrderModel>? DrawOrders { get; set; }
}
