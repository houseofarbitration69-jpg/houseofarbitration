#region Imports
using CommunityToolkit.Mvvm.ComponentModel;
#endregion

namespace House.Of.Arbitration.Models;

public partial class CompetitorCategoryModel : ObservableObject
{
    /// <summary>
    /// Obtient ou définit l'identifiant de la catégorie
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'identifiant du compétiteur
    /// </summary>
    public required int CompetitorId { get; set; }

    /// <summary>
    /// Obtient ou définit le compétiteur
    /// </summary>
    public CompetitorModel? Competitor { get; set; }

    /// <summary>
    /// Obtient ou définit l'identifiant de la catégorie
    /// </summary>
    public required int CategoryId { get; set; }

    /// <summary>
    /// Obtient ou définit la catégorie
    /// </summary>
    public CategoryModel? Category { get; set; }

    /// <summary>
    /// Obtient ou définit la liste des alertes de la catégorie
    /// </summary>
    public List<WarningModel>? Warnings { get; set; }
}