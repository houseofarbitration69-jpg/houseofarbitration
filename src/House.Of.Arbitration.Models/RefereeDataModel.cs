namespace House.Of.Arbitration.Models;

public class RefereeDataModel
{
    public int Id { get; set; } = 0;
    public string Referee { get; set; } = String.Empty;
    public required DateTime Date { get; set; }
    public string Data { get; set; } = String.Empty;
}
