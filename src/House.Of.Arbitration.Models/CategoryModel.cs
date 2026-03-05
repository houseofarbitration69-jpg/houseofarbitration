namespace House.Of.Arbitration.Models;

public class CategoryModel
{
    public int Id { get; set; } = -1;
    public CategoryType Type { get; set; }
    public Genre Genre { get; set; }
    public int WeightMin { get; set; }
    public int WeightMax { get; set; }
    public AgeRange AgeRange { get; set; }
    public int CompetitionId { get; set; }
    public required CompetitionModel Competition { get; set; }
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