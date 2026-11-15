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
}
