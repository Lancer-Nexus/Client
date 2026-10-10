using System.Text;

namespace Nexus.Assets;

public static class NapPath
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0 || path.IndexOf('\0') >= 0 || path.Contains(':') ||
            path.StartsWith('/') || path.StartsWith('\\') || Path.IsPathRooted(path))
            throw new InvalidDataException("NAP paths must be relative virtual paths.");

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("EXE/../DATA/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[12..];
        else if (normalized.StartsWith("../DATA/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[8..];
        else if (normalized.StartsWith("DATA/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[5..];

        var parts = normalized.Split('/');
        if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.Length > 255 ||
                           p.EndsWith(' ') || p.EndsWith('.') || p.Any(c => c < 0x20 || "<>\"|?*".Contains(c)) ||
                           IsDeviceName(p)))
            throw new InvalidDataException("NAP path contains an empty, dot, traversal, or oversized component.");
        if (StrictUtf8.GetByteCount(normalized) > NapLimits.MaxPathBytes)
            throw new InvalidDataException("NAP path is too long.");
        return string.Join('/', parts);
    }

    private static bool IsDeviceName(string component)
    {
        var stem = component.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                                     stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                stem[3] is >= '1' and <= '9');
    }
}

internal static class NapLimits
{
    public const int HeaderLength = 128;
    public const int ChunkRecordLength = 64;
    public const int MaxIndexLength = 64 * 1024 * 1024;
    public const int MaxEntries = 1_000_000;
    public const int MaxChunks = 4_000_000;
    public const int MaxPathBytes = 4096;
    public const int MaxChunkSize = 1024 * 1024;
    public const int MaxCompressedChunkSize = MaxChunkSize + 64 * 1024;
}
