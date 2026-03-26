namespace House.Of.Arbitration.Models;

public class WarningModel
{
    /// <summary>
    /// Obtient ou définit l'identifiant du compétiteur
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'identifiant du compétiteur correspondant a l'alerte
    /// </summary>
    public int? CompetitorId { get; set; }

    /// <summary>
    /// Obtient ou définit le compétiteur correspondant a l'alerte
    /// </summary>
    public CompetitorModel? Competitor { get; set; }

    /// <summary>
    /// Obtient ou définit l'identifiant de la catégorie correspondant a l'alerte
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Obtient ou définit la catégorie correspondant a l'alerte
    /// </summary>
    public CategoryModel? Category { get; set; }

    /// <summary>
    /// Obtient ou définit a quoi correspond l'alerte
    /// </summary>
    public required string Label { get; set; }
}
