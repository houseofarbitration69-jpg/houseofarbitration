namespace House.Of.Arbitration.Models;

public class DrawOrderModel
{
    /// <summary>
    /// Obtient ou définit l'identifiant du tirage en mode 'Pools'
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'ordre du match dans la catégorie
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'ordre du match dans la compétition
    /// </summary>
    public int GlobalOrder { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit l'identifiant du competiteur
    /// </summary>
    public int? CompetitorId { get; set; }

    /// <summary>
    /// Obtient ou définit le compétiteur
    /// </summary>
    public CompetitorModel? Competitor { get; set; }

    /// <summary>
    /// Obtient ou définit l'identifiant du tirage
    /// </summary>
    public int DrawId { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit le tirage
    /// </summary>
    public required DrawModel Draw { get; set; }

    /// <summary>
    /// Obtient ou définit la liste des données des arbitres
    /// </summary>
    public List<RefereeDataModel>? RefereeDatas { get; set; }
}
