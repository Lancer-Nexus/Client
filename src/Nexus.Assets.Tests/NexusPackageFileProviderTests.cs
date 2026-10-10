using System.Security.Cryptography;
using System.Text.Json;
using LibreLancer.Data.IO;
using Nexus.Assets;
using Xunit;

namespace Nexus.Assets.Tests;

public sealed class NexusPackageFileProviderTests
{
    [Fact]
    public void MissingActiveSnapshotLeavesStandaloneDataFilesystemAlone()
    {
        using var fixture = new Fixture();
        Assert.Null(NexusPackageFileProvider.LoadActive(fixture.ApplicationDirectory));
    }

    [Fact]
    public void ActivePackagesOverrideFallbackMaskFilesAndEnumerateEffectiveDirectories()
    {
        using var fixture = new Fixture();
        fixture.WriteInstall("ships/data.ini", "base version");
        fixture.WriteInstall("ships/base-only.ini", "base only");
        fixture.WriteInstall("removal/sub/removed.ini", "remove me");
        fixture.WriteInstall("removal/keep.ini", "keep me");
        fixture.WritePackage("ships/data.ini", "package version"u8.ToArray());
        fixture.WritePackage("ships/new.ini", "new package file"u8.ToArray());
        fixture.AddTombstone("removal/sub/removed.ini");
        _ = fixture.Pack();
        fixture.Activate();

        var provider = NexusPackageFileProvider.LoadActive(fixture.ApplicationDirectory);
        Assert.NotNull(provider);
        var fs = FileSystem.FromPath(fixture.InstallDirectory);
        fs.FileProviders.Add(provider);

        Assert.Equal("package version", fs.ReadAllText("EXE/../DATA/ships/data.ini"));
        Assert.Equal("package version", fs.ReadAllText("EXE\\..\\data//ships/data.ini"));
        Assert.Equal("base only", fs.ReadAllText("DATA/ships/base-only.ini"));
        Assert.False(fs.FileExists("DATA/removal/sub/removed.ini"));
        Assert.Throws<FileNotFoundException>(() => fs.Open("DATA/removal/sub/removed.ini"));
        Assert.Null(fs.GetBackingFileName("DATA/ships/data.ini"));
        Assert.NotNull(fs.GetBackingFileName("DATA/ships/base-only.ini"));
        Assert.Equal(new[] { "base-only.ini", "data.ini", "new.ini" }.Order(StringComparer.OrdinalIgnoreCase),
            fs.GetFiles("DATA/ships").Order(StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain("sub", fs.GetDirectories("DATA/removal"));
        Assert.Equal("keep.ini", Assert.Single(fs.GetFiles("DATA/removal")));
    }

    [Fact]
    public void EditorWorkspaceProvidesWritablePackageOverridesAndPersistentEnumeration()
    {
        using var fixture = new Fixture();
        fixture.WriteInstall("ships/data.ini", "loose fallback");
        fixture.WritePackage("ships/data.ini", "packaged source"u8.ToArray());
        fixture.WritePackage("ships/base-only.ini", "packaged base"u8.ToArray());
        _ = fixture.Pack();
        fixture.Activate();

        var packageProvider = NexusPackageFileProvider.LoadActive(fixture.ApplicationDirectory)!;
        var workspacePath = Path.Combine(fixture.Root, "editor-workspace");
        var workspace = new NexusPackageWorkspaceFileProvider(workspacePath, packageProvider);
        var fileSystem = FileSystem.FromPath(fixture.InstallDirectory);
        fileSystem.FileProviders.Add(packageProvider);
        fileSystem.FileProviders.Add(workspace);

        var backingPath = fileSystem.GetBackingFileName("EXE/../DATA/ships/data.ini");
        Assert.NotNull(backingPath);
        Assert.StartsWith(workspacePath, backingPath, StringComparison.Ordinal);
        Assert.Equal("packaged source", fileSystem.ReadAllText("DATA/ships/data.ini"));
        Assert.True(workspace.GetBackingFileName("DATA/SHIPS", out var backingDirectory));
        Assert.True(Directory.Exists(backingDirectory));

        File.WriteAllText(backingPath, "editor revision");
        fileSystem.Refresh();
        Assert.Equal("editor revision", fileSystem.ReadAllText("../DATA/SHIPS/DATA.INI"));
        Assert.Equal(new[] { "base-only.ini", "data.ini" }.Order(StringComparer.OrdinalIgnoreCase),
            fileSystem.GetFiles("DATA/ships").Order(StringComparer.OrdinalIgnoreCase));

        var newPackagePath = fileSystem.GetBackingFileName("DATA/ships/new.ini");
        Assert.Null(newPackagePath);
        Assert.False(workspace.GetBackingFileName("DATA/../../outside.ini", out _));
        Assert.Null(workspace.Open("DATA/../../outside.ini"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "outside.ini")));
    }

    [Fact]
    public void PackageConflictsRequireExplicitOverrideAndMountOrderIsUnique()
    {
        using var fixture = new Fixture();
        fixture.WritePackage("same.ini", [1]);
        var first = NapArchive.Open(fixture.Pack("first.nap"));
        fixture.WritePackage("same.ini", [2]);
        var second = NapArchive.Open(fixture.Pack("second.nap"));
        var empty = new HashSet<string>(StringComparer.Ordinal);

        Assert.Throws<InvalidDataException>(() => new NexusPackageFileProvider(
        [
            new NexusPackageMount("base", first, 10, 1, empty),
            new NexusPackageMount("hotfix", second, 20, 1, empty)
        ]));

        Assert.Throws<InvalidDataException>(() => new NexusPackageFileProvider(
        [
            new NexusPackageMount("base", first, 10, 1, empty),
            new NexusPackageMount("another", second, 10, 1, empty)
        ]));

        var overlay = new NexusPackageFileProvider(
        [
            new NexusPackageMount("base", first, 10, 1, empty),
            new NexusPackageMount("hotfix", second, 20, 1, new HashSet<string>(["base"], StringComparer.Ordinal))
        ]);
        using var stream = overlay.Open("same.ini");
        Assert.Equal(2, stream!.ReadByte());
    }

    [Fact]
    public void ActiveSnapshotRejectsPackageHashMismatchAndPathTraversal()
    {
        using var fixture = new Fixture();
        fixture.WritePackage("asset.dat", [1, 2, 3]);
        var path = fixture.Pack("core.nap");
        fixture.Activate();
        var damaged = File.ReadAllBytes(path);
        damaged[^1] ^= 0xff;
        File.WriteAllBytes(path, damaged);
        Assert.Throws<InvalidDataException>(() => NexusPackageFileProvider.LoadActive(fixture.ApplicationDirectory));

        fixture.Activate(relativePackagePath: "../outside.nap");
        Assert.Throws<InvalidDataException>(() => NexusPackageFileProvider.LoadActive(fixture.ApplicationDirectory));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "nap-mount-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string packageSource;
        private readonly List<string> tombstones = [];
        public string Root => root;
        public string ApplicationDirectory => Path.Combine(root, "application");
        public string InstallDirectory => Path.Combine(root, "freelancer");
        private string PackagesDirectory => Path.Combine(ApplicationDirectory, "packages");

        public Fixture()
        {
            packageSource = Path.Combine(root, "source");
            Directory.CreateDirectory(packageSource);
            Directory.CreateDirectory(Path.Combine(InstallDirectory, "EXE"));
            File.WriteAllText(Path.Combine(InstallDirectory, "EXE", "freelancer.ini"), "[Freelancer]");
        }

        public void WriteInstall(string path, string contents)
        {
            var full = Path.Combine(InstallDirectory, "DATA", path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, contents);
        }

        public void WritePackage(string path, byte[] contents)
        {
            var full = Path.Combine(packageSource, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, contents);
        }

        public void AddTombstone(string path)
        {
            tombstones.Add(path);
        }

        public string Pack(string file = "core.nap")
        {
            Directory.CreateDirectory(PackagesDirectory);
            var output = Path.Combine(PackagesDirectory, file);
            var entries = Directory.EnumerateFiles(packageSource, "*", SearchOption.AllDirectories)
                .Select(path => new NapSourceEntry(
                    Path.GetRelativePath(packageSource, path).Replace(Path.DirectorySeparatorChar, '/'), path))
                .Concat(tombstones.Select(path => new NapSourceEntry(path, null)));
            NapWriter.Pack(entries, output);
            return output;
        }

        public void Activate(string relativePackagePath = "core.nap")
        {
            Directory.CreateDirectory(PackagesDirectory);
            var packagePath = Path.Combine(PackagesDirectory, relativePackagePath);
            var metadata = new FileInfo(packagePath);
            var hash = metadata.Exists
                ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(packagePath))).ToLowerInvariant()
                : new string('0', 64);
            var active = new
            {
                schemaVersion = 1,
                packages = new[]
                {
                    new
                    {
                        id = "core",
                        path = relativePackagePath,
                        size = metadata.Exists ? metadata.Length : 1,
                        sha256 = hash,
                        required = true,
                        priority = 100,
                        mountOrder = 1,
                        dependencies = Array.Empty<string>(),
                        overrides = Array.Empty<string>()
                    }
                }
            };
            File.WriteAllText(Path.Combine(PackagesDirectory, "active.json"), JsonSerializer.Serialize(active));
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
