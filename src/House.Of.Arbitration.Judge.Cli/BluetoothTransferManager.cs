using System.IO.Compression;
using System.Text;

namespace House.Of.Arbitration.Judge.Cli;

/// <summary>
/// Gestionnaire pour la compression GZip et le découpage en paquets (chunking) des messages Bluetooth LE.
/// </summary>
public class BluetoothTransferManager
{
    private const int MaxPayloadSize = 400;
    private const int CompressionThreshold = 100;
    private const string ZipPrefix = "GZ:";
    private const string ChunkPrefix = "CH:";

    private readonly Dictionary<string, string?[]> _pendingChunks = new();

    /// <summary>
    /// Prépare un message pour l'envoi : compression GZip si utile et découpage en paquets.
    /// </summary>
    public IEnumerable<string> PrepareMessagesForSending(string message)
    {
        string finalMessage = message;

        // 1. Compression GZip si le message est assez long
        if (message.Length > CompressionThreshold)
        {
            finalMessage = ZipPrefix + Compress(message);
        }

        // 2. Découpage si dépasse la taille MTU
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

                // Format: CH:ID:INDEX:TOTAL:PAYLOAD
                yield return $"{ChunkPrefix}{messageId}:{i}:{totalChunks}:{payload}";
            }
        }
    }

    /// <summary>
    /// Traite les données reçues. Reconstitue les paquets découpés et décompresse si nécessaire.
    /// </summary>
    public string? ProcessReceivedData(string data)
    {
        if (data.StartsWith(ChunkPrefix))
        {
            var parts = data.Substring(ChunkPrefix.Length).Split(':', 4);
            if (parts.Length < 4) return null;

            string id = parts[0];
            if (!int.TryParse(parts[1], out int index) || !int.TryParse(parts[2], out int total))
                return null;

            string payload = parts[3];

            if (!_pendingChunks.ContainsKey(id))
            {
                _pendingChunks[id] = new string?[total];
            }

            var chunks = _pendingChunks[id];
            if (index >= 0 && index < total)
            {
                chunks[index] = payload;
            }

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
    /// Réinitialise les paquets en attente (par ex. lors d'une déconnexion).
    /// </summary>
    public void Clear()
    {
        _pendingChunks.Clear();
    }

    private string FinalizeMessage(string message)
    {
        if (message.StartsWith(ZipPrefix))
        {
            try
            {
                return Decompress(message.Substring(ZipPrefix.Length));
            }
            catch
            {
                return message;
            }
        }
        return message;
    }

    private static string Compress(string text)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        using var ms = new MemoryStream();
        using (var zip = new GZipStream(ms, CompressionMode.Compress, false))
        {
            zip.Write(buffer, 0, buffer.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string Decompress(string compressedText)
    {
        try
        {
            byte[] buffer = Convert.FromBase64String(compressedText);
            using var ms = new MemoryStream(buffer);
            using var zip = new GZipStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(zip, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            return $"DECOMPRESS_ERROR:{ex.Message}";
        }
    }
}
