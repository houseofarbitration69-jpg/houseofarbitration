namespace House.Of.Arbitration.Models;

/// <summary>
/// Résultat d'un match terminé diffusé aux moniteurs.
/// </summary>
public class MatchResultModel
{
    public int MatchId { get; set; }
    public int? WinnerId { get; set; }
    public string? WinnerName { get; set; }
    public Constants.Side? WinnerSide { get; set; }
    public double RedScore { get; set; }
    public double BlueScore { get; set; }
    public string? Reason { get; set; }
    public bool IsFinished { get; set; } = true;
}
