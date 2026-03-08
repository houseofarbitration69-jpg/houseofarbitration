namespace House.Of.Arbitration.Models;

public class CompetitorModel
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = string.Empty;
    public Genre Genre { get; set; } = Genre.None;
    public int CategoryId { get; set; }
    public CategoryModel? Category { get; set; }
}
