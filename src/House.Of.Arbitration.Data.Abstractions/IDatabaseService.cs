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
}
