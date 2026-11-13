#if ANDROID
using Android.Content;
using Android.OS;
using Android.Provider;
using House.Of.Arbitration.Services.Abstractions;
using Environment = Android.OS.Environment;

namespace House.Of.Arbitration.Services.Platforms.Android;

/// <summary>
/// Implémentation Android du service de téléchargement de fichiers.
/// Utilise MediaStore sur API 29+ et le dossier public Downloads sur les versions antérieures.
/// </summary>
public class FileDownloadService : IFileDownloadService
{
    public async Task<string?> SaveToDownloadsAsync(Stream stream, string fileName, string mimeType)
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                // Android 10+ : MediaStore (pas besoin de permission WRITE_EXTERNAL_STORAGE)
                var contentValues = new ContentValues();
                contentValues.Put(MediaStore.IMediaColumns.DisplayName, fileName);
                contentValues.Put(MediaStore.IMediaColumns.MimeType, mimeType);
#pragma warning disable CA1416
                contentValues.Put(MediaStore.IMediaColumns.RelativePath, Environment.DirectoryDownloads);
#pragma warning restore CA1416

                var resolver = global::Android.App.Application.Context.ContentResolver;
                // MediaStore.Downloads.ExternalContentUri et RelativePath sont API 29+ — garanti par le check SdkInt >= Q ci-dessus
#pragma warning disable CA1416
                var uri = resolver!.Insert(MediaStore.Downloads.ExternalContentUri!, contentValues);
#pragma warning restore CA1416
                if (uri == null) return null;

                using var output = resolver.OpenOutputStream(uri);
                if (output == null) return null;

                stream.Position = 0;
                await stream.CopyToAsync(output);

                // Retourner un chemin lisible (informatif)
                return $"Téléchargements/{fileName}";
            }
            else
            {
                // Android < 10 : dossier public Downloads (nécessite WRITE_EXTERNAL_STORAGE)
                var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
                if (status != PermissionStatus.Granted) return null;

                var downloadsDir = Environment.GetExternalStoragePublicDirectory(
                    Environment.DirectoryDownloads)!.AbsolutePath;
                var filePath = Path.Combine(downloadsDir, fileName);

                stream.Position = 0;
                using var fs = File.Create(filePath);
                await stream.CopyToAsync(fs);

                return filePath;
            }
        }
        catch
        {
            return null;
        }
    }
}
#endif
