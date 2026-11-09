using System.IO.Compression;
using System.Text;

namespace House.Of.Arbitration.Server.Cli;

/// <summary>
/// Gestionnaire de découpage (chunks) et compression (gzip) pour les messages Bluetooth.
/// </summary>
public class BluetoothTransferManager
{
    private const int MaxPayloadSize = 400;
    private const int CompressionThreshold = 100;
    private const string ZipPrefix = "GZ:";
    private const string ChunkPrefix = "CH:";

    private readonly Dictionary<string, string?[]> _pendingChunks = new();

    /// <summary>
    /// Prépare un message texte pour l'envoi : compresse si pertinent et découpe en chunks si supérieur au MTU.
    /// </summary>
    public IEnumerable<string> PrepareMessagesForSending(string message)
    {
        string finalMessage = message;

        if (message.Length > CompressionThreshold)
        {
            finalMessage = ZipPrefix + Compress(message);
        }

        if (finalMessage.Length <= MaxPayloadSize)
        {
            yield return finalMessage;
        }
        else
        {
            string messageId = Guid.NewGuid().ToString("N").Substring(0, 4);
            int totalChunks = (int)Math.Ceiling((double)finalMessage.Length / MaxPayloadSize);

            for (int i = 0; i < totalChunks; i++)
            {
                int start = i * MaxPayloadSize;
                int length = Math.Min(MaxPayloadSize, finalMessage.Length - start);
                string payload = finalMessage.Substring(start, length);

                yield return $"{ChunkPrefix}{messageId}:{i}:{totalChunks}:{payload}";
            }
        }
    }

    /// <summary>
    /// Traite les données reçues. Reconstitue les chunks et décompresse si nécessaire.
    /// Renvoie le message complet quand il est assemblé, sinon null.
    /// </summary>
    public string? ProcessReceivedData(string data)
    {
        if (data.StartsWith(ChunkPrefix))
        {
            var parts = data.Substring(ChunkPrefix.Length).Split(':', 4);
            if (parts.Length < 4) return null;

            string id = parts[0];
            int index = int.Parse(parts[1]);
            int total = int.Parse(parts[2]);
            string payload = parts[3];

            if (!_pendingChunks.ContainsKey(id))
            {
                _pendingChunks[id] = new string?[total];
            }

            var chunks = _pendingChunks[id];
            chunks[index] = payload;

            if (chunks.All(c => c != null))
            {
                string assembled = string.Join("", chunks);
                _pendingChunks.Remove(id);
                return FinalizeMessage(assembled);
            }
            return null;
        }

        return FinalizeMessage(data);
    }

    /// <summary>
    /// Réinitialise les chunks en attente.
    /// </summary>
    public void Clear()
    {
        _pendingChunks.Clear();
    }

    private string FinalizeMessage(string message)
    {
        if (message.StartsWith(ZipPrefix))
        {
            return Decompress(message.Substring(ZipPrefix.Length));
        }

        return message;
    }

    private static string Compress(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    private static string Decompress(string compressedBase64)
    {
        byte[] bytes = Convert.FromBase64String(compressedBase64);
        using var memoryStream = new MemoryStream(bytes);
        using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
