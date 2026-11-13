namespace House.Of.Arbitration.Services.Abstractions;

/// <summary>
/// Service permettant de sauvegarder un fichier dans le dossier Téléchargements selon la plateforme
/// </summary>
public interface IFileDownloadService
{
    /// <summary>
    /// Sauvegarde le flux dans le dossier Téléchargements de l'appareil.
    /// Retourne le chemin du fichier créé, ou null en cas d'échec.
    /// </summary>
    Task<string?> SaveToDownloadsAsync(Stream stream, string fileName, string mimeType);
}
