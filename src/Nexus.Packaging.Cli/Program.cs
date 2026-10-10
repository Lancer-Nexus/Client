using Nexus.Assets;
using LibreLancer.Data.IO;
using System.Text.Json;

static int Usage()
{
    Console.Error.WriteLine("Usage:\n  nexus-pack pack <source-dir> <output.nap> [--version N] [--chunk-size BYTES] [--compression-level N]\n  nexus-pack inspect <file.nap>\n  nexus-pack verify <file.nap>\n  nexus-pack diff <base.nap> <target.nap> <patch.nap> <base-package-id>\n  nexus-pack compact <application-dir> <output.nap> [--version N] [--workspace <directory>]");
    return 2;
}

try
{
    if (args.Length < 2) return Usage();
    switch (args[0])
    {
        case "pack":
            {
                if (args.Length < 3) return Usage();
                var options = new NapPackOptions();
                for (var i = 3; i < args.Length; i += 2)
                {
                    if (i + 1 >= args.Length) return Usage();
                    var value = args[i + 1];
                    options = args[i] switch
                    {
                        "--version" when ulong.TryParse(value, out var v) => options with { ContentVersion = v },
                        "--chunk-size" when int.TryParse(value, out var c) => options with { ChunkSize = c },
                        "--compression-level" when int.TryParse(value, out var l) => options with { CompressionLevel = l },
                        _ => throw new ArgumentException($"Invalid option or value: {args[i]}")
                    };
                }
                NapWriter.PackDirectory(args[1], args[2], options);
                Console.WriteLine($"Packed {Path.GetFullPath(args[1])} -> {Path.GetFullPath(args[2])}");
                return 0;
            }
        case "inspect" when args.Length == 2:
            {
                var archive = NapArchive.Open(args[1]);
                Console.WriteLine($"Package: {archive.PackageId:D}\nContent version: {archive.ContentVersion}\nEntries: {archive.Entries.Count}");
                foreach (var entry in archive.Entries.OrderBy(e => e.Path, StringComparer.Ordinal))
                    Console.WriteLine($"{(entry.Tombstone ? "DELETE" : "FILE  ")} {entry.Length,12} {Convert.ToHexString(entry.Sha256).ToLowerInvariant()} {entry.Path}");
                return 0;
            }
        case "verify" when args.Length == 2:
            {
                var archive = NapArchive.Open(args[1]);
                archive.VerifyAll();
                Console.WriteLine($"Verified {archive.Entries.Count} entries in {Path.GetFullPath(args[1])}");
                return 0;
            }
        case "diff" when args.Length == 5:
            {
                var basePath = Path.GetFullPath(args[1]);
                var targetPath = Path.GetFullPath(args[2]);
                var outputPath = Path.GetFullPath(args[3]);
                var pathComparer = OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
                if (pathComparer.Equals(outputPath, basePath) || pathComparer.Equals(outputPath, targetPath) ||
                    pathComparer.Equals(outputPath + ".plan.json", basePath) ||
                    pathComparer.Equals(outputPath + ".plan.json", targetPath))
                    throw new ArgumentException("Diff output and its sidecar must not replace either input archive.");
                var result = NapOverlayBuilder.BuildDiff(
                    NapArchive.Open(basePath), NapArchive.Open(targetPath), args[4], outputPath);
                var plan = new
                {
                    schemaVersion = 1,
                    packageGuid = result.PackageGuid,
                    basePackageId = result.BasePackageId,
                    dependencies = new[] { result.BasePackageId },
                    overrides = new[] { result.BasePackageId },
                    contentVersion = result.ContentVersion,
                    changedFiles = result.ChangedFiles,
                    tombstones = result.Tombstones
                };
                WriteJsonAtomically(outputPath + ".plan.json", plan);
                Console.WriteLine($"Built {outputPath}: {result.ChangedFiles} changed files, {result.Tombstones} tombstones.");
                Console.WriteLine($"The signed release manifest must declare dependencies/overrides: {result.BasePackageId}.");
                return 0;
            }
        case "compact" when args.Length >= 3 && args.Length <= 7 && (args.Length - 3) % 2 == 0:
            {
                ulong version = 1;
                string? workspaceDirectory = null;
                for (var i = 3; i < args.Length; i += 2)
                {
                    if (args[i] == "--version" && ulong.TryParse(args[i + 1], out var parsedVersion))
                        version = parsedVersion;
                    else if (args[i] == "--workspace" && !string.IsNullOrWhiteSpace(args[i + 1]))
                        workspaceDirectory = Path.GetFullPath(args[i + 1]);
                    else
                        return Usage();
                }
                var applicationDirectory = Path.GetFullPath(args[1]);
                var outputPath = Path.GetFullPath(args[2]);
                var relativeOutput = Path.GetRelativePath(applicationDirectory, outputPath);
                if (!Path.IsPathRooted(relativeOutput) && relativeOutput != ".." &&
                    !relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new ArgumentException("Compaction output must be outside the active application directory.");
                var provider = NexusPackageFileProvider.LoadActive(applicationDirectory)
                    ?? throw new InvalidDataException("Application directory has no active NAP package snapshot.");
                var fileSystem = new FileSystem(provider);
                if (workspaceDirectory is not null)
                {
                    var relativeWorkspaceOutput = Path.GetRelativePath(workspaceDirectory, outputPath);
                    if (!Path.IsPathRooted(relativeWorkspaceOutput) && relativeWorkspaceOutput != ".." &&
                        !relativeWorkspaceOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        throw new ArgumentException("Compaction output must be outside the editor workspace.");
                    fileSystem.FileProviders.Add(new NexusPackageWorkspaceFileProvider(workspaceDirectory, provider));
                }
                var packageGuid = NapOverlayBuilder.Compact(fileSystem, outputPath, version);
                Console.WriteLine($"Compacted the effective package tree to {outputPath} (package {packageGuid:D}).");
                return 0;
            }
        default:
            return Usage();
    }
}
catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException or OverflowException or InvalidOperationException)
{
    Console.Error.WriteLine($"nexus-pack: {exception.Message}");
    return 1;
}

static void WriteJsonAtomically<T>(string outputPath, T value)
{
    var path = Path.GetFullPath(outputPath);
    var directory = Path.GetDirectoryName(path)!;
    Directory.CreateDirectory(directory);
    var temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
    try
    {
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(output, value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            output.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }
    finally
    {
        if (File.Exists(temporary)) File.Delete(temporary);
    }
}
