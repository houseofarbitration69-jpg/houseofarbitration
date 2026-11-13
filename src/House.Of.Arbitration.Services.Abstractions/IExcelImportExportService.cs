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
}
