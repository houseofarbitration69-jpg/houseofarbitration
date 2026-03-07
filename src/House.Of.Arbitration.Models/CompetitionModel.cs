namespace House.Of.Arbitration.Models;

public class CompetitionModel
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = String.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public List<CategoryModel> Categories { get; set; } = new();
}
