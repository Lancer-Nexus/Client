using System;
using System.Collections.Generic;
using System.IO;
using LibreLancer.Data.IO;

namespace LLServer;

/// <summary>Maps the Freelancer data path in EXE/freelancer.ini to packaged LLServer overrides.</summary>
public sealed class NpcDataOverlayFileProvider : IFileProvider
{
    private const string ExeRelativeDataPrefix = "EXE/../data/";
    private const string RelativeDataPrefix = "../data/";
    private const string RootDataPrefix = "data/";
    private readonly SysFolderQuickInit files;

    public NpcDataOverlayFileProvider(string dataDirectory)
    {
        files = new SysFolderQuickInit(dataDirectory);
    }

    private static string? Map(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith(ExeRelativeDataPrefix, StringComparison.OrdinalIgnoreCase))
            return normalized[ExeRelativeDataPrefix.Length..];
        if (normalized.StartsWith(RelativeDataPrefix, StringComparison.OrdinalIgnoreCase))
            return normalized[RelativeDataPrefix.Length..];
        if (normalized.StartsWith(RootDataPrefix, StringComparison.OrdinalIgnoreCase))
            return normalized[RootDataPrefix.Length..];
        return null;
    }

    public Stream? Open(string filename)
    {
        var mapped = Map(filename);
        return mapped is null ? null : files.Open(mapped);
    }

    public bool FileExists(string filename)
    {
        var mapped = Map(filename);
        return mapped is not null && files.FileExists(mapped);
    }

    public bool GetBackingFileName(string path, out string? fileName)
    {
        var mapped = Map(path);
        if (mapped is not null)
            return files.GetBackingFileName(mapped, out fileName);
        fileName = null;
        return false;
    }

    public IEnumerable<string> GetFiles(string path)
    {
        var mapped = Map(path);
        return mapped is null ? Array.Empty<string>() : files.GetFiles(mapped);
    }

    public IEnumerable<string> GetDirectories(string path)
    {
        var mapped = Map(path);
        return mapped is null ? Array.Empty<string>() : files.GetDirectories(mapped);
    }

    public void Refresh() => files.Refresh();
}
