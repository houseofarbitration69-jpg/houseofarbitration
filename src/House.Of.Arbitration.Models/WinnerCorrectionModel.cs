namespace House.Of.Arbitration.Models;

/// <summary>
/// Correction de vainqueur d'un match (envoyée par le Monitor au serveur).
/// </summary>
public class WinnerCorrectionModel
{
    /// <summary>Identifiant du match à corriger.</summary>
    public int MatchId { get; set; }

    /// <summary>Côté déclaré vainqueur (Red ou Blue).</summary>
    public Constants.Side WinningSide { get; set; }

    /// <summary>Identifiant du compétiteur vainqueur (optionnel, pour validation).</summary>
    public int? WinnerCompetitorId { get; set; }

    /// <summary>Raison de la correction (pour audit).</summary>
    public string? Reason { get; set; }
}
