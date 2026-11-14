namespace House.Of.Arbitration.Services.Abstractions;

/// <summary>
/// Résultat d'une opération d'import Excel
/// </summary>
public class ImportResult
{
    /// <summary>Indique si l'import s'est déroulé sans erreur bloquante</summary>
    public bool Success { get; set; }
    /// <summary>Nombre de catégories importées avec succès</summary>
    public int ImportedCategories { get; set; }
    /// <summary>Nombre de compétiteurs importés avec succès</summary>
    public int ImportedCompetitors { get; set; }
    /// <summary>Nombre de tirages générés avec succès</summary>
    public int ImportedDraws { get; set; }
    /// <summary>Liste des erreurs bloquantes (validation échouée, format invalide...)</summary>
    public List<string> Errors { get; set; } = new();
    /// <summary>Liste des avertissements non-bloquants</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Données d'une catégorie pour l'export Excel des résultats
/// </summary>
public class CategoryResultExportData
{
    /// <summary>Nom de la catégorie</summary>
    public string CategoryName { get; set; } = string.Empty;
    /// <summary>Type de la catégorie (ex: Sanda, Taolu)</summary>
    public string CategoryType { get; set; } = string.Empty;
    /// <summary>Liste des compétiteurs classés pour cette catégorie</summary>
    public List<CompetitorResultExportRow> Rows { get; set; } = new();
}

/// <summary>
/// Ligne de résultat d'un compétiteur pour l'export Excel
/// </summary>
public class CompetitorResultExportRow
{
    public int Position { get; set; }
    public string Medal { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Score { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Données d'une catégorie pour l'export Excel des notes arbitres
/// </summary>
public class CategoryRefereeExportData
{
    /// <summary>Nom de la catégorie</summary>
    public string CategoryName { get; set; } = string.Empty;
    /// <summary>Lignes de notes triées chronologiquement</summary>
    public List<RefereeScoreExportRow> Rows { get; set; } = new();
}

/// <summary>
/// Ligne de note d'arbitre pour l'export Excel
/// </summary>
public class RefereeScoreExportRow
{
    public DateTime Date { get; set; }
    public TimeSpan? MatchTimer { get; set; }
    public string MatchTimerDisplay => MatchTimer.HasValue ? $"{(int)MatchTimer.Value.TotalMinutes:D2}:{MatchTimer.Value.Seconds:D2}" : "00:00";
    public string Referee { get; set; } = string.Empty;
    public string CompetitorLastName { get; set; } = string.Empty;
    public string CompetitorFirstName { get; set; } = string.Empty;
    public double Score { get; set; }
    public int Round { get; set; }
    public bool IsCorrection { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public int? MatchId { get; set; }
    public int? GlobalOrder { get; set; }
}

/// <summary>
/// Service d'import/export Excel pour les compétitions
/// </summary>
public interface IExcelImportExportService
{
    /// <summary>
    /// Génère et retourne un fichier Excel template vide prêt à être rempli
    /// </summary>
    Task<Stream> ExportTemplateAsync();

    /// <summary>
    /// Importe un fichier Excel rempli et insère les données en base
    /// </summary>
    /// <param name="excelStream">Flux du fichier .xlsx</param>
    /// <returns>Résultat de l'import avec détails des erreurs/avertissements</returns>
    Task<ImportResult> ImportAsync(Stream excelStream);

    /// <summary>
    /// Génère un fichier Excel des résultats avec un onglet par catégorie
    /// </summary>
    Task<Stream> ExportResultsAsync(string competitionName, DateTime? competitionDate, IEnumerable<CategoryResultExportData> categories);

    /// <summary>
    /// Génère un fichier Excel des notes des arbitres avec un onglet par catégorie.
    /// Chaque ligne correspond à une note donnée par un arbitre à un compétiteur,
    /// avec l'horodatage, le round, l'identifiant du match et le flag de correction.
    /// </summary>
    /// <param name="competitionName">Nom de la compétition</param>
    /// <param name="competitionDate">Date de la compétition</param>
    /// <param name="categories">Données par catégorie (notes triées chronologiquement)</param>
    Task<Stream> ExportRefereeDataAsync(string competitionName, DateTime? competitionDate, IEnumerable<CategoryRefereeExportData> categories);
}
