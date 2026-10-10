using System.IO;
using LibreLancer.Data.IO;

namespace LancerEdit.GameContent;

/// <summary>Resolves a VFS asset to its writable loose or editor-workspace path.</summary>
internal static class WritableDataPath
{
    public static string Resolve(FileSystem vfs, string virtualPath)
    {
        var path = vfs.GetBackingFileName(virtualPath);
        if (!string.IsNullOrWhiteSpace(path)) return path;
        throw new IOException($"No writable backing file is available for '{virtualPath}'.");
    }
}
