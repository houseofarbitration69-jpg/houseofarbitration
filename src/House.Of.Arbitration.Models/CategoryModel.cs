namespace House.Of.Arbitration.Models;

public class CategoryModel
{
    public int Id { get; set; } = 0;
    public CategoryType Type { get; set; }
    public Genre Genre { get; set; }
    public int WeightMin { get; set; }
    public int WeightMax { get; set; }
    public AgeRange AgeRange { get; set; }
    public int? CompetitionId { get; set; }
    public RoundType RoundType { get; set; }
    public CompetitionModel? Competition { get; set; }
    public List<CompetitorModel> Competitors { get; set; } = new();
}

public enum CategoryType
{
    None,
    Sanda,
    SandaLight,
    Taolu
}

public enum Genre
{
    None,
    Men,
    Women
}

public enum AgeRange
{
    None, 
    Cadets,
    Juniors,
    Seniors,
    Espoirs,
    Veterans
}

public enum RoundType
{
    None,
    Elimination,
    Robin
}
