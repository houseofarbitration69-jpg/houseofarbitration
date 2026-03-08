namespace House.Of.Arbitration.Models;

public class CompetitorModel
{
    public int Id { get; set; } = 0;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Name => $"{FirstName} {LastName}".Trim();
    
    public Genre Genre { get; set; } = Genre.None;
    public DateTime BirthDate { get; set; } = DateTime.Now.AddYears(-20);
    public string Club { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public double CurrentWeight { get; set; }
    
    public int? CategoryId { get; set; }
    public CategoryModel? Category { get; set; }
}
