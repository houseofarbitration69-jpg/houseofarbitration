namespace House.Of.Arbitration.Models;

public class CompetitionModel
{
    public int Id { get; set; } = -1;
    public required string Name { get; set; }
    public required DateOnly Date { get; set; }
    public string? Address { get; set; }
}
