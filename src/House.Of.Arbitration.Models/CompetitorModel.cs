namespace House.Of.Arbitration.Models;

public class CompetitorModel
{
    /// <summary>
    /// Obtient ou définit l'identifiant du compétiteur
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit le prénom du compétiteur
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Obtient ou définit le nom du compétiteur
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Obtient le nom complet du compétiteur
    /// </summary>
    public string Name => $"{FirstName} {LastName}".Trim();
    
    /// <summary>
    /// Obtient ou définit le genre du compétiteur
    /// </summary>
    public Genre Genre { get; set; } = Genre.None;

    /// <summary>
    /// Obtient ou définit la date de naissance du compétiteur
    /// </summary>
    public DateTime BirthDate { get; set; } = DateTime.Now.AddYears(-20);
    
    /// <summary>
    /// Obtient ou définit le nom du club du compétiteur
    /// </summary>
    public string Club { get; set; } = string.Empty;

    /// <summary>
    /// Obtient ou définit le poids du compétiteurs
    /// </summary>
    public double Weight { get; set; }
    
    /// <summary>
    /// Obtient ou définit la liste des catégoriesdu compétiteur
    /// </summary>
    public List<CompetitorCategoryModel>? Categories { get; set; }
}
