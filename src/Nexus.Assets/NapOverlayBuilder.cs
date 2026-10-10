namespace Nexus.Assets;

public sealed record NapDiffResult(string BasePackageId, Guid PackageGuid, int ChangedFiles, int Tombstones,
    ulong ContentVersion);

/// <summary>Builds versioned overlay packages and compacts an effective package mount into one NAP.</summary>
public static class NapOverlayBuilder
{
    public static NapDiffResult BuildDiff(NapArchive baseline, NapArchive target, string basePackageId,
        string outputFile)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(target);
        ValidatePackageId(basePackageId);
        baseline.VerifyAll();
        target.VerifyAll();

        var oldFiles = baseline.Entries.Where(e => !e.Tombstone)
            .ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var newFiles = target.Entries.Where(e => !e.Tombstone)
            .ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        var changed = newFiles.Values.Where(entry =>
                !oldFiles.TryGetValue(entry.Path, out var previous) ||
                previous.Length != entry.Length || !previous.Sha256.AsSpan().SequenceEqual(entry.Sha256))
            .OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
        var deleted = oldFiles.Keys.Where(path => !newFiles.ContainsKey(path))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "nexus-nap-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            var entries = new List<NapSourceEntry>(changed.Length + deleted.Length);
            foreach (var entry in changed)
            {
                var destination = Path.Combine(temporaryRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var source = target.OpenFile(entry.Path);
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(output);
                output.Flush(flushToDisk: true);
                entries.Add(new NapSourceEntry(entry.Path, destination));
            }
            entries.AddRange(deleted.Select(path => new NapSourceEntry(path, null)));
            NapWriter.Pack(entries, outputFile, new NapPackOptions { ContentVersion = target.ContentVersion });
            var result = NapArchive.Open(outputFile);
            result.VerifyAll();
            return new NapDiffResult(basePackageId, result.PackageId, changed.Length, deleted.Length, target.ContentVersion);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    public static Guid Compact(NexusPackageFileProvider provider, string outputFile, ulong contentVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return Compact(new LibreLancer.Data.IO.FileSystem(provider), outputFile, contentVersion);
    }

    public static Guid Compact(LibreLancer.Data.IO.FileSystem fileSystem, string outputFile,
        ulong contentVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "nexus-nap-compact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CopyDirectory("", temporaryRoot, fileSystem, visited);
            NapWriter.PackDirectory(temporaryRoot, outputFile, new NapPackOptions { ContentVersion = contentVersion });
            var archive = NapArchive.Open(outputFile);
            archive.VerifyAll();
            return archive.PackageId;
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static void CopyDirectory(string virtualDirectory, string outputDirectory,
        LibreLancer.Data.IO.FileSystem fileSystem, HashSet<string> visited)
    {
        var normalized = virtualDirectory.Replace('\\', '/').Trim('/');
        if (!visited.Add(normalized)) return;
        foreach (var file in fileSystem.GetFiles(virtualDirectory))
        {
            var virtualPath = normalized.Length == 0 ? file : normalized + "/" + file;
            var safePath = NapPath.Normalize(virtualPath);
            var destination = Path.Combine(outputDirectory, safePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = fileSystem.Open(virtualPath);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
        foreach (var directory in fileSystem.GetDirectories(virtualDirectory))
        {
            var childVirtual = normalized.Length == 0 ? directory : normalized + "/" + directory;
            var childOutput = Path.Combine(outputDirectory, directory);
            Directory.CreateDirectory(childOutput);
            CopyDirectory(childVirtual, outputDirectory, fileSystem, visited);
        }
    }

    private static void ValidatePackageId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 96 ||
            id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new ArgumentException("Base package ID is invalid.", nameof(id));
    }
}
