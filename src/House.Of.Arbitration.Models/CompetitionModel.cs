namespace House.Of.Arbitration.Models;

public class CompetitionModel
{
    public int Id { get; set; } = -1;
    public string Name { get; set; } = String.Empty;
    public  DateOnly Date { get; set; } = new DateOnly();
    public List<CategoryModel> Categories { get; set; } = new();
}
