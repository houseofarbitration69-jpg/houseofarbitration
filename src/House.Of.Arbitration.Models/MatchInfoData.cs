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
}
