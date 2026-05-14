using System.IO.Compression;
using System.Text;

namespace House.Of.Arbitration.Services;

/// <summary>
/// A helper class to handle zipping/unzipping and chunking/unchunking of Bluetooth messages.
/// This ensures that large payloads are transmitted reliably and efficiently.
/// </summary>
public class BluetoothTransferManager
{
    private const int MaxPayloadSize = 400;
    private const int CompressionThreshold = 100;
    private const string ZipPrefix = "GZ:";
    private const string ChunkPrefix = "CH:";

    private readonly Dictionary<string, string?[]> _pendingChunks = new();

    /// <summary>
    /// Prepares a message for sending: zips if useful and splits into chunks if too large.
    /// </summary>
    public IEnumerable<string> PrepareMessagesForSending(string message)
    {
        string finalMessage = message;

        // 1. Zip if the message is long enough to benefit from it
        if (message.Length > CompressionThreshold)
        {
            finalMessage = ZipPrefix + Compress(message);
        }

        // 2. Split into chunks if exceeds MTU-safe size
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
    /// Processes received data. If it's a chunk, it's buffered. 
    /// Returns the full message when reassembly is complete, or null if more chunks are expected.
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
    /// Clears any pending chunks (e.g. on disconnect).
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
                // Fallback or log? For now, return as is if decompression fails
                return message;
            }
        }
        return message;
    }

    private string Compress(string text)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(text);
        using var ms = new MemoryStream();
        using (var zip = new GZipStream(ms, CompressionMode.Compress, false))
        {
            zip.Write(buffer, 0, buffer.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    private string Decompress(string compressedText)
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
            System.Diagnostics.Debug.WriteLine($"Decompression error: {ex.Message}");
            return $"DECOMPRESS_ERROR:{compressedText}";
        }
    }
}
