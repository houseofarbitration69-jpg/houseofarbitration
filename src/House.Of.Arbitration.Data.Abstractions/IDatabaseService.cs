using House.Of.Arbitration.Models;

namespace House.Of.Arbitration.Data.Abstractions;

/// <summary>
/// Service de gestion de la base de données
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Vide la base de données des données insérées après l'installation tout en conservant les données par défaut.
    /// </summary>
    Task ResetUserDataAsync();

    /// <summary>
    /// Sauvegarde ou met à jour l'ensemble des données d'une compétition (catégories, compétiteurs, tirages, etc.).
    /// </summary>
    /// <param name="competition">Modèle complet de la compétition à importer</param>
    Task ImportCompetitionDataAsync(CompetitionModel competition);
}
