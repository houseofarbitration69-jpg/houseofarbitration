namespace House.Of.Arbitration.Models;

public class MatchInfoData
{
    /// <summary>
    /// Obtient ou définit l'identifiant du match
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Obtient ou définit le type du match
    /// </summary>
    public RoundType Type { get; set; }

    /// <summary>
    /// Obtient ou définit le numéro de round en cours (1 par défaut)
    /// </summary>
    public int Round { get; set; } = 1;

    /// <summary>
    /// Identifiant de la catégorie du match
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Nom de la catégorie du match
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Étape du match (ex: 1/4 finale, 1/2 finale, Poule - Match #1)
    /// </summary>
    public string? StageName { get; set; }

    /// <summary>
    /// Premier compétiteur (Rouge / Athlète)
    /// </summary>
    public CompetitorModel? Competitor1 { get; set; }

    /// <summary>
    /// Deuxième compétiteur (Bleu)
    /// </summary>
    public CompetitorModel? Competitor2 { get; set; }

    /// <summary>
    /// Données complètes du match si mode élimination directe (Knockouts)
    /// </summary>
    public DrawKnockoutModel? DrawKnockout { get; set; }

    /// <summary>
    /// Données complètes du match si mode poules (Pools)
    /// </summary>
    public DrawPoolsModel? DrawPool { get; set; }

    /// <summary>
    /// Données complètes du match si mode ordre de passage (Order / Taolu)
    /// </summary>
    public DrawOrderModel? DrawOrder { get; set; }

    /// <summary>
    /// Indique si le passage Taolu est ouvert (notation autorisée par le serveur).
    /// Toujours <c>false</c> pour les modes Sanda (chrono utilisé à la place).
    /// </summary>
    public bool IsPassageOpen { get; set; }
}
