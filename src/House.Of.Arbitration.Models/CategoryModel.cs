namespace House.Of.Arbitration.Models;

public class CategoryModel
{
    /// <summary>
    /// Obtient ou définit l'identifiant de la catégorie
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// Obtient ou définit le type de la catégorie
    /// </summary>
    public CategoryType Type { get; set; }

    /// <summary>
    /// Obtient ou définit le genre de la catégorie
    /// </summary>
    public Genre Genre { get; set; }

    /// <summary>
    /// Obtient ou définit le poids minimum de la catégorie
    /// </summary>
    public int WeightMin { get; set; }

    /// <summary>
    /// Obtient ou définit le poids maximum de la catégorie
    /// </summary>
    public int WeightMax { get; set; }

    /// <summary>
    /// Obtient ou définit la plage d'age de la catégorie
    /// </summary>
    public AgeRange AgeRange { get; set; }

    /// <summary>
    /// Obtient ou définit le type de round de la catégorie
    /// </summary>
    public RoundType RoundType { get; set; }

    /// <summary>
    /// Obtient ou définit l'identifiant de la compétition de la catégorie
    /// </summary>
    public int? CompetitionId { get; set; }
    
    /// <summary>
    /// Obtient ou définit la compétition de la catégorie
    /// </summary>
    public CompetitionModel? Competition { get; set; }

    /// <summary>
    /// Obtient ou définit la liste des compétiteurs de la catégorie
    /// </summary>
    public List<CompetitorModel> Competitors { get; set; } = new();

    /// <summary>
    /// Obtient ou définit la liste des alertes de la catégorie
    /// </summary>
    public List<WarningModel>? Warnings { get; set; }
}

/// <summary>
/// Enumération des types de catégorie
/// </summary>
public enum CategoryType
{
    None,
    Sanda,
    SandaLight,
    Taolu
}

/// <summary>
/// Enumération des genres
/// </summary>
public enum Genre
{
    None,
    Men,
    Women,
    Mixte
}

/// <summary>
/// Enumération des plages d'age
/// </summary>
public enum AgeRange
{
    None, 
    Cadets,
    Juniors,
    Seniors,
    Espoirs,
    Veterans
}

/// <summary>
/// Enumération des types de round
/// </summary>
public enum RoundType
{
    None,
    Knockouts,
    Pools,
    Order
}
