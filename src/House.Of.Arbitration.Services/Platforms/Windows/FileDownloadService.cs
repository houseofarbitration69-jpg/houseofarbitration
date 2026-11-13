#if WINDOWS
using House.Of.Arbitration.Services.Abstractions;

namespace House.Of.Arbitration.Services.Platforms.Windows;

/// <summary>
/// Implémentation Windows du service de téléchargement de fichiers.
/// Enregistre directement dans le dossier Téléchargements de l'utilisateur.
/// </summary>
public class FileDownloadService : IFileDownloadService
{
    public async Task<string?> SaveToDownloadsAsync(Stream stream, string fileName, string mimeType)
    {
        try
        {
            var downloadsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            if (!Directory.Exists(downloadsFolder))
                downloadsFolder = FileSystem.Current.CacheDirectory;

            var filePath = Path.Combine(downloadsFolder, fileName);

            stream.Position = 0;
            using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream);

            return filePath;
        }
        catch
        {
            return null;
        }
    }
}
#endif
