using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Services.Abstractions;

/// <summary>
/// Service d'export PDF des tirages de compétition
/// </summary>
public interface IDrawPdfExportService
{
    /// <summary>
    /// Génère un PDF du tableau éliminatoire (arbre knockout) pour une liste de tirages.
    /// </summary>
    /// <param name="draw">Le tirage principal (avec Category)</param>
    /// <param name="knockouts">Liste des matchs éliminatoires</param>
    /// <param name="competitionName">Nom de la compétition</param>
    /// <returns>Stream du fichier PDF généré</returns>
    Task<Stream> ExportKnockoutBracketAsync(DrawModel draw, List<DrawKnockoutModel> knockouts, string competitionName);

    /// <summary>
    /// Génère un PDF des poules (round-robin) pour une liste de tirages.
    /// </summary>
    /// <param name="draw">Le tirage principal (avec Category)</param>
    /// <param name="pools">Liste des matchs de poule</param>
    /// <param name="competitionName">Nom de la compétition</param>
    /// <returns>Stream du fichier PDF généré</returns>
    Task<Stream> ExportPoolsAsync(DrawModel draw, List<DrawPoolsModel> pools, string competitionName);

    /// <summary>
    /// Génère un PDF de l'ordre de passage et des notes taolu.
    /// </summary>
    /// <param name="draw">Le tirage principal (avec Category)</param>
    /// <param name="orders">Liste des passages taolu</param>
    /// <param name="competitionName">Nom de la compétition</param>
    /// <returns>Stream du fichier PDF généré</returns>
    Task<Stream> ExportTaoluOrderAsync(DrawModel draw, List<DrawOrderModel> orders, string competitionName);

    /// <summary>
    /// Génère un PDF complet de tous les tirages de la compétition (éliminatoires + poules + taolu).
    /// </summary>
    /// <param name="draws">Liste de tous les tirages</param>
    /// <param name="competitionName">Nom de la compétition</param>
    /// <returns>Stream du fichier PDF généré</returns>
    Task<Stream> ExportAllDrawsAsync(List<DrawModel> draws, string competitionName);
}
