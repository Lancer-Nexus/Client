using System;
using System.IO;
using System.Text;

namespace LibreLancer.Server;

/// <summary>Records that a committed NPC snapshot entered this server process's live simulation.</summary>
public static class NpcTransferActivationReceipt
{
    public static bool IsRecorded(string directory, Guid transferId)
    {
        return Read(directory, transferId) is not null;
    }

    public static bool IsRecorded(string directory, Guid transferId, Guid generation)
    {
        if (generation == Guid.Empty)
            throw new ArgumentException("NPC activation generation cannot be empty.", nameof(generation));
        return Read(directory, transferId)?.Generation == generation;
    }

    public static void Record(string directory, Guid transferId)
    {
        Write(directory, transferId, null);
    }

    public static void Record(string directory, Guid transferId, Guid generation)
    {
        if (generation == Guid.Empty)
            throw new ArgumentException("NPC activation generation cannot be empty.", nameof(generation));
        if (IsRecorded(directory, transferId, generation))
            return;
        Write(directory, transferId, generation);
    }

    private static void Write(string directory, Guid transferId, Guid? generation)
    {
        var path = GetPath(directory, transferId);
        Directory.CreateDirectory(directory);
        if (generation is null && IsRecorded(directory, transferId))
            return;

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var value = generation is { } recordGeneration
                ? $"{transferId:N}:{recordGeneration:N}"
                : transferId.ToString("N");
            var bytes = Encoding.ASCII.GetBytes(value);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            try { File.Move(temporary, path, overwrite: true); }
            catch (IOException) when (File.Exists(path))
            {
                if (generation is { } currentGeneration
                        ? !IsRecorded(directory, transferId, currentGeneration)
                        : !IsRecorded(directory, transferId))
                    throw;
            }
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { }
        }
    }

    private static Receipt? Read(string directory, Guid transferId)
    {
        var path = GetPath(directory, transferId);
        if (!File.Exists(path))
            return null;
        var content = File.ReadAllText(path, Encoding.ASCII);
        var id = transferId.ToString("N");
        if (string.Equals(content, id, StringComparison.Ordinal))
            return new Receipt(transferId, null); // Previous versions persisted an unscoped receipt.
        if (content.Length == 65 && content[32] == ':' &&
            string.Equals(content[..32], id, StringComparison.Ordinal) &&
            Guid.TryParseExact(content[33..], "N", out var generation) && generation != Guid.Empty)
            return new Receipt(transferId, generation);
        throw new InvalidDataException("NPC transfer activation receipt is corrupt.");
    }

    private sealed record Receipt(Guid TransferId, Guid? Generation);

    private static string GetPath(string directory, Guid transferId)
    {
        if (string.IsNullOrWhiteSpace(directory) || transferId == Guid.Empty)
            throw new ArgumentException("NPC activation receipt identity is invalid.");
        return Path.Combine(directory, $"{transferId:N}.activated");
    }
}
