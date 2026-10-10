using LibreLancer.Data.IO;

namespace Nexus.Assets;

/// <summary>Persistent loose-file edits layered above a package snapshot for editor workflows.</summary>
public sealed class NexusPackageWorkspaceFileProvider : IOverlayFileProvider
{
    private readonly string root;
    private readonly SysFolderQuickInit files;
    private readonly NexusPackageFileProvider packages;

    public NexusPackageWorkspaceFileProvider(string workspaceDirectory, NexusPackageFileProvider packages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(packages);
        root = Path.GetFullPath(workspaceDirectory);
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("NAP editor workspace cannot be a symbolic link or reparse point.");
        files = new SysFolderQuickInit(root);
        this.packages = packages;
    }

    public string WorkspaceDirectory => root;

    public Stream? Open(string filename)
    {
        if (!TryNormalize(filename, out var normalized) || !TryResolveExisting(normalized, out var path) ||
            !File.Exists(path))
            return null;
        return File.OpenRead(path);
    }

    public bool FileExists(string filename) =>
        TryNormalize(filename, out var normalized) &&
        TryResolveExisting(normalized, out var path) && File.Exists(path);

    public bool GetBackingFileName(string path, out string? fileName)
    {
        fileName = null;
        if (!TryNormalize(path, out var normalized)) return false;
        if (TryResolveExisting(normalized, out var existing) &&
            (File.Exists(existing) || Directory.Exists(existing)))
        {
            fileName = existing;
            return true;
        }

        var canonical = packages.GetCanonicalFilePath(normalized) ??
                       packages.GetCanonicalDirectoryPath(normalized);
        if (canonical is null) return false;
        var destination = Path.GetFullPath(Path.Combine(root,
            canonical.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(destination) || !HasNoReparsePointAncestor(destination)) return false;
        Directory.CreateDirectory(packages.GetCanonicalDirectoryPath(normalized) is not null
            ? destination
            : Path.GetDirectoryName(destination)!);
        if (!HasNoReparsePointAncestor(destination)) return false;
        fileName = destination;
        return true;
    }

    public IEnumerable<string> GetFiles(string path)
    {
        if (!TryNormalize(path, out var normalized) || !TryResolveExisting(normalized, out var directory) ||
            !Directory.Exists(directory))
            return Array.Empty<string>();
        return files.GetFiles(normalized).Where(name => IsSafeChild(directory, name, isDirectory: false)).ToArray();
    }

    public IEnumerable<string> GetDirectories(string path)
    {
        if (!TryNormalize(path, out var normalized) || !TryResolveExisting(normalized, out var directory) ||
            !Directory.Exists(directory))
            return Array.Empty<string>();
        return files.GetDirectories(normalized).Where(name => IsSafeChild(directory, name, isDirectory: true)).ToArray();
    }

    public bool HasTombstonesUnder(string directory) => false;

    public bool TryResolveOverlayFile(string filename, out bool exists)
    {
        exists = FileExists(filename);
        return exists;
    }

    public void Refresh() => files.Refresh();

    private bool TryNormalize(string path, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0) return false;
        var value = path.Replace('\\', '/');
        if (value.Contains("//", StringComparison.Ordinal))
        {
            var collapsed = new System.Text.StringBuilder(value.Length);
            var previousWasSlash = false;
            foreach (var character in value)
            {
                if (character == '/' && previousWasSlash) continue;
                collapsed.Append(character);
                previousWasSlash = character == '/';
            }
            value = collapsed.ToString();
        }
        foreach (var prefix in new[] { "EXE/../DATA/", "../DATA/", "DATA/" })
        {
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            value = value[prefix.Length..];
            break;
        }
        if (value.Equals("DATA", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("../DATA", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("EXE/../DATA", StringComparison.OrdinalIgnoreCase))
            value = "";
        value = value.TrimEnd('/');
        if (value.Length == 0) return true;
        try
        {
            normalized = NapPath.Normalize(value);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private bool TryResolveExisting(string relativePath, out string path)
    {
        path = root;
        if (files.GetBackingFileName(relativePath, out var resolved) && resolved is not null &&
            IsWithinRoot(resolved) && HasNoReparsePointAncestor(resolved))
        {
            path = resolved;
            return true;
        }
        return relativePath.Length == 0 && HasNoReparsePointAncestor(root);
    }

    private bool IsSafeChild(string parent, string name, bool isDirectory)
    {
        if (string.IsNullOrEmpty(name) || name.Contains('/') || name.Contains('\\')) return false;
        var path = Path.Combine(parent, name);
        return IsWithinRoot(path) && HasNoReparsePointAncestor(path) &&
               (isDirectory ? Directory.Exists(path) : File.Exists(path));
    }

    private bool IsWithinRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, fullPath);
        return !Path.IsPathRooted(relative) && relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private bool HasNoReparsePointAncestor(string path)
    {
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return false;
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return false;
        var current = root;
        foreach (var component in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (component.Length == 0 || component == ".") continue;
            current = Path.Combine(current, component);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        }
        return true;
    }
}
