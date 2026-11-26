namespace House.Of.Arbitration.Models;

/// <summary>
/// Correction du score d'un juge pour un match (envoyée par le Monitor au serveur).
/// </summary>
public class ScoreCorrectionModel
{
    /// <summary>Identifiant du match à corriger.</summary>
    public int MatchId { get; set; }

    /// <summary>Position du juge à corriger (ex: "1", "A_2").</summary>
    public string JudgePosition { get; set; } = string.Empty;

    /// <summary>Côté (compétiteur) concerné par la correction.</summary>
    public Constants.Side? Side { get; set; }

    /// <summary>Identifiant du compétiteur concerné.</summary>
    public int? CompetitorId { get; set; }

    /// <summary>Nouveau score à appliquer.</summary>
    public double NewScore { get; set; }

    /// <summary>Numéro de round concerné (1 par défaut).</summary>
    public int Round { get; set; } = 1;

    /// <summary>Raison de la correction (pour audit).</summary>
    public string? Reason { get; set; }
}
