using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LibreLancer.Data.IO;

namespace Nexus.Assets;

public sealed class NexusPackageFileProvider : IOverlayFileProvider
{
    private sealed record Package(string Id, NapArchive Archive, int Priority, int MountOrder,
        HashSet<string> Overrides);
    private sealed record EffectiveEntry(Package Package, NapEntry Entry);
    private sealed record ActiveSnapshot
    {
        public int SchemaVersion { get; init; }
        public ActivePackage[]? Packages { get; init; }
    }
    private sealed record ActivePackage
    {
        public string? Id { get; init; }
        public string? Path { get; init; }
        public long Size { get; init; }
        public string? Sha256 { get; init; }
        public bool Required { get; init; }
        public ulong? Version { get; init; }
        public int Priority { get; init; }
        public int MountOrder { get; init; }
        public string[]? Dependencies { get; init; }
        public string[]? Overrides { get; init; }
    }

    private readonly Dictionary<string, EffectiveEntry> files;
    private readonly HashSet<string> tombstoneParentDirectories;

    public NexusPackageFileProvider(IEnumerable<NexusPackageMount> mounts)
    {
        ArgumentNullException.ThrowIfNull(mounts);
        var requested = mounts.ToArray();
        if (requested.Any(x => x is null))
            throw new ArgumentException("Package mounts cannot contain null entries.", nameof(mounts));
        var sorted = requested.OrderBy(x => x.Priority)
            .ThenBy(x => x.MountOrder)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();
        if (sorted.Length == 0) throw new ArgumentException("At least one NAP package is required.", nameof(mounts));

        var packageIds = new HashSet<string>(StringComparer.Ordinal);
        var mountOrders = new HashSet<(int Priority, int MountOrder)>();
        foreach (var mount in sorted)
        {
            if (mount is null || mount.Overrides is null)
                throw new ArgumentException("Package mounts and override lists cannot be null.", nameof(mounts));
            ValidateId(mount.Id);
            if (!packageIds.Add(mount.Id)) throw new InvalidDataException($"Duplicate NAP package ID: {mount.Id}");
            if (!mountOrders.Add((mount.Priority, mount.MountOrder)))
                throw new InvalidDataException("Packages at the same priority require distinct mount orders.");
        }
        foreach (var mount in sorted)
            foreach (var overrideId in mount.Overrides)
                if (!packageIds.Contains(overrideId) || overrideId == mount.Id)
                    throw new InvalidDataException($"Package {mount.Id} has an invalid override reference: {overrideId}");

        files = new Dictionary<string, EffectiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var mount in sorted)
        {
            var package = new Package(mount.Id, mount.Archive, mount.Priority, mount.MountOrder,
                new HashSet<string>(mount.Overrides, StringComparer.Ordinal));
            foreach (var entry in mount.Archive.Entries)
            {
                if (files.TryGetValue(entry.Path, out var previous) && !package.Overrides.Contains(previous.Package.Id))
                    throw new InvalidDataException(
                        $"Package {package.Id} conflicts with {previous.Package.Id} at {entry.Path} without an explicit override.");
                files[entry.Path] = new EffectiveEntry(package, entry);
            }
        }
        tombstoneParentDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in files.Values.Where(x => x.Entry.Tombstone))
        {
            var directory = entry.Entry.Path;
            var separator = directory.LastIndexOf('/');
            directory = separator < 0 ? "" : directory[..separator];
            while (true)
            {
                tombstoneParentDirectories.Add(directory);
                var parentSeparator = directory.LastIndexOf('/');
                if (parentSeparator < 0) break;
                directory = directory[..parentSeparator];
            }
        }
    }

    /// <summary>Loads a local activated snapshot from &lt;applicationDirectory&gt;/packages/active.json.</summary>
    public static NexusPackageFileProvider? LoadActive(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        var root = Path.GetFullPath(Path.Combine(applicationDirectory, "packages"));
        var activePath = Path.Combine(root, "active.json");
        if (!File.Exists(activePath)) return null;
        EnsureNoReparsePoints(root, activePath);
        var info = new FileInfo(activePath);
        if (info.Length is <= 0 or > 4 * 1024 * 1024)
            throw new InvalidDataException("Active NAP snapshot metadata has an invalid size.");

        ActiveSnapshot snapshot;
        using (var input = new FileStream(activePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            snapshot = JsonSerializer.Deserialize<ActiveSnapshot>(input,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("Active NAP snapshot metadata is empty.");
        if (snapshot.SchemaVersion != 1 || snapshot.Packages is null || snapshot.Packages.Length > 1024)
            throw new InvalidDataException("Active NAP snapshot schema or package count is invalid.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Packages)
        {
            if (item.Id is null) throw new InvalidDataException("Active NAP package ID is missing.");
            ValidateId(item.Id);
            if (!ids.Add(item.Id)) throw new InvalidDataException($"Duplicate active NAP package ID: {item.Id}");
        }
        foreach (var item in snapshot.Packages)
        {
            foreach (var dependency in item.Dependencies ?? [])
                if (!ids.Contains(dependency) || dependency == item.Id)
                    throw new InvalidDataException($"Package {item.Id} has a missing or invalid dependency: {dependency}");
            foreach (var overridden in item.Overrides ?? [])
                if (!ids.Contains(overridden) || overridden == item.Id)
                    throw new InvalidDataException($"Package {item.Id} has an invalid override reference: {overridden}");
        }

        var mounts = new List<NexusPackageMount>(snapshot.Packages.Length);
        foreach (var item in snapshot.Packages)
        {
            var packagePath = ResolvePackagePath(root, item.Path);
            EnsureNoReparsePoints(root, packagePath);
            var fileInfo = new FileInfo(packagePath);
            if (!fileInfo.Exists || item.Size <= 0 || fileInfo.Length != item.Size)
                throw new InvalidDataException($"Active NAP package size does not match: {item.Id}");
            if (item.Sha256 is null || item.Sha256.Length != 64 || !IsHex(item.Sha256))
                throw new InvalidDataException($"Active NAP package hash is invalid: {item.Id}");
            using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] actualHash = SHA256.HashData(packageStream);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, Convert.FromHexString(item.Sha256)))
                throw new InvalidDataException($"Active NAP package hash mismatch: {item.Id}");

            // Parse the exact file handle whose bytes were hashed; an atomic path replacement
            // between verification and parsing must not select a different archive.
            var archive = NapArchive.Open(packageStream, packagePath);
            if (item.Version.HasValue && item.Version.Value != archive.ContentVersion)
                throw new InvalidDataException($"Active NAP package version does not match: {item.Id}");
            mounts.Add(new NexusPackageMount(item.Id!, archive, item.Priority, item.MountOrder,
                new HashSet<string>(item.Overrides ?? [], StringComparer.Ordinal)));
        }
        return mounts.Count == 0 ? null : new NexusPackageFileProvider(mounts);
    }

    public Stream? Open(string filename)
    {
        if (!TryGet(filename, out var effective) || effective.Entry.Tombstone) return null;
        return effective.Package.Archive.OpenFile(effective.Entry.Path);
    }

    public bool FileExists(string filename) =>
        TryGet(filename, out var effective) && !effective.Entry.Tombstone;

    public bool ContainsDirectory(string path)
    {
        if (!TryNormalizeDirectory(path, out var directory)) return false;
        var prefix = directory.Length == 0 ? "" : directory + "/";
        return files.Any(pair => !pair.Value.Entry.Tombstone &&
            pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && pair.Key.Length > prefix.Length);
    }

    public string? GetCanonicalFilePath(string path) =>
        TryGet(path, out var effective) && !effective.Entry.Tombstone ? effective.Entry.Path : null;

    public string? GetCanonicalDirectoryPath(string path)
    {
        if (!TryNormalizeDirectory(path, out var directory) || !ContainsDirectory(directory)) return null;
        if (directory.Length == 0) return "";
        var prefix = directory + "/";
        var matchingPath = files.Values.Where(x => !x.Entry.Tombstone)
            .Select(x => x.Entry.Path)
            .FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (matchingPath is null) return null;
        return matchingPath[..matchingPath.IndexOf('/', prefix.Length - 1)];
    }

    public bool GetBackingFileName(string path, out string? fileName)
    {
        fileName = null;
        return TryGet(path, out _);
    }

    public bool TryResolveOverlayFile(string filename, out bool exists)
    {
        if (TryGet(filename, out var effective))
        {
            exists = !effective.Entry.Tombstone;
            return true;
        }
        exists = false;
        return false;
    }

    public bool HasTombstonesUnder(string directory) =>
        TryNormalizeDirectory(directory, out var normalized) &&
        tombstoneParentDirectories.Contains(normalized);

    public IEnumerable<string> GetFiles(string path)
    {
        if (!TryNormalizeDirectory(path, out var directory)) return Array.Empty<string>();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in files)
        {
            if (pair.Value.Entry.Tombstone || !TryDirectChild(directory, pair.Key, out var child, out var isFile))
                continue;
            if (isFile) result.Add(child);
        }
        return result;
    }

    public IEnumerable<string> GetDirectories(string path)
    {
        if (!TryNormalizeDirectory(path, out var directory)) return Array.Empty<string>();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in files)
        {
            if (pair.Value.Entry.Tombstone || !TryDirectChild(directory, pair.Key, out var child, out var isFile))
                continue;
            if (!isFile) result.Add(child);
        }
        return result;
    }

    public void Refresh() { }

    private bool TryGet(string path, out EffectiveEntry effective)
    {
        try
        {
            var key = NormalizeFilePath(path);
            return files.TryGetValue(key, out effective!);
        }
        catch (InvalidDataException)
        {
            effective = null!;
            return false;
        }
    }

    private static string NormalizeFilePath(string path)
    {
        var value = StripDataRoot(path);
        return NapPath.Normalize(value);
    }

    private static bool TryNormalizeDirectory(string path, out string directory)
    {
        try
        {
            var value = StripDataRoot(path).TrimEnd('/');
            directory = value.Length == 0 ? "" : NapPath.Normalize(value);
            return true;
        }
        catch (InvalidDataException)
        {
            directory = "";
            return false;
        }
    }

    private static string StripDataRoot(string path)
    {
        var value = path.Replace('\\', '/');
        if (value.Contains("//", StringComparison.Ordinal))
        {
            var normalized = new StringBuilder(value.Length);
            var previousWasSlash = false;
            foreach (var character in value)
            {
                if (character == '/' && previousWasSlash) continue;
                normalized.Append(character);
                previousWasSlash = character == '/';
            }
            value = normalized.ToString();
        }
        foreach (var prefix in new[] { "EXE/../DATA/", "../DATA/", "DATA/" })
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value[prefix.Length..];
        if (value.Equals("DATA", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("../DATA", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("EXE/../DATA", StringComparison.OrdinalIgnoreCase))
            return "";
        return value;
    }

    private static bool TryDirectChild(string directory, string path, out string child, out bool isFile)
    {
        var prefix = directory.Length == 0 ? "" : directory + "/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || path.Length <= prefix.Length)
        {
            child = "";
            isFile = false;
            return false;
        }
        var remainder = path[prefix.Length..];
        var separator = remainder.IndexOf('/');
        isFile = separator < 0;
        child = isFile ? remainder : remainder[..separator];
        return child.Length != 0;
    }

    private static string ResolvePackagePath(string packagesRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\0') || relativePath.Contains(':') ||
            relativePath.StartsWith('/') || relativePath.StartsWith('\\'))
            throw new InvalidDataException("Active NAP package path must be relative.");
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Split('/').Any(x => x.Length == 0 || x is "." or ".."))
            throw new InvalidDataException("Active NAP package path contains an unsafe component.");
        var fullPath = Path.GetFullPath(Path.Combine(packagesRoot,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(packagesRoot, fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Active NAP package path escapes its package root.");
        return fullPath;
    }

    private static void EnsureNoReparsePoints(string packagesRoot, string filePath)
    {
        if (!Directory.Exists(packagesRoot) || (File.GetAttributes(packagesRoot) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Active NAP package root is missing or is a reparse point.");
        var relative = Path.GetRelativePath(packagesRoot, filePath);
        var current = packagesRoot;
        foreach (var component in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, component);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Active NAP metadata and packages cannot use symbolic links or reparse points.");
        }
    }

    private static void ValidateId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 96 ||
            id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new InvalidDataException($"NAP package ID is invalid: {id}");
    }

    private static bool IsHex(string value) => value.All(Uri.IsHexDigit);
}

public sealed record NexusPackageMount(string Id, NapArchive Archive, int Priority, int MountOrder,
    IReadOnlyCollection<string> Overrides);
