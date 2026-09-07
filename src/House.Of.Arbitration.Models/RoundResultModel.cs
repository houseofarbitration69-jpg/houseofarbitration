namespace House.Of.Arbitration.Models;

public class RoundResultModel
{
    public int RoundNumber { get; set; }
    public int RedVotes { get; set; }
    public int BlueVotes { get; set; }
    public string WinnerColor { get; set; } = string.Empty;
}
