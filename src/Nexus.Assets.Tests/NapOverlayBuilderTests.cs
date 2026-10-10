using LibreLancer.Data.IO;
using Xunit;

namespace Nexus.Assets.Tests;

public sealed class NapOverlayBuilderTests
{
    [Fact]
    public void DiffContainsOnlyChangedFilesAndDeletions()
    {
        using var fixture = new Fixture();
        var baselinePath = fixture.PackDirectory("baseline", new Dictionary<string, string>
        {
            ["same.ini"] = "same",
            ["change.ini"] = "old",
            ["removed.ini"] = "remove"
        });
        var targetPath = fixture.PackDirectory("target", new Dictionary<string, string>
        {
            ["same.ini"] = "same",
            ["change.ini"] = "new",
            ["added.ini"] = "add"
        }, 7);
        var patchPath = Path.Combine(fixture.Root, "update.nap");

        var result = NapOverlayBuilder.BuildDiff(NapArchive.Open(baselinePath), NapArchive.Open(targetPath),
            "base-data", patchPath);
        var patch = NapArchive.Open(patchPath);
        patch.VerifyAll();

        Assert.Equal(2, result.ChangedFiles);
        Assert.Equal(1, result.Tombstones);
        Assert.Equal(7UL, result.ContentVersion);
        Assert.Equal(new[] { "added.ini", "change.ini", "removed.ini" },
            patch.Entries.Select(x => x.Path).Order(StringComparer.Ordinal).ToArray());
        Assert.True(Assert.Single(patch.Entries, x => x.Path == "removed.ini").Tombstone);
        Assert.DoesNotContain(patch.Entries, x => x.Path == "same.ini");
        using var changed = patch.OpenFile("change.ini");
        using var reader = new StreamReader(changed);
        Assert.Equal("new", reader.ReadToEnd());
    }

    [Fact]
    public void DiffMountedOnBaselineProducesTheExactTargetVirtualTree()
    {
        using var fixture = new Fixture();
        var baselinePath = fixture.PackDirectory("diff-baseline", new Dictionary<string, string>
        {
            ["keep/base.ini"] = "base",
            ["change.ini"] = "old",
            ["remove/nested.ini"] = "remove"
        });
        var targetPath = fixture.PackDirectory("diff-target", new Dictionary<string, string>
        {
            ["keep/base.ini"] = "base",
            ["change.ini"] = "new",
            ["add/nested.ini"] = "added"
        }, 9);
        var patchPath = Path.Combine(fixture.Root, "diff-patch.nap");

        NapOverlayBuilder.BuildDiff(NapArchive.Open(baselinePath), NapArchive.Open(targetPath),
            "baseline", patchPath);
        var provider = new NexusPackageFileProvider(
        [
            new NexusPackageMount("baseline", NapArchive.Open(baselinePath), 1, 1,
                new HashSet<string>(StringComparer.Ordinal)),
            new NexusPackageMount("update", NapArchive.Open(patchPath), 2, 1,
                new HashSet<string>(["baseline"], StringComparer.Ordinal))
        ]);
        var fileSystem = new FileSystem(provider);
        var target = NapArchive.Open(targetPath);
        var actualPaths = EnumerateFiles(fileSystem, "").Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(target.Entries.Select(x => x.Path).Order(StringComparer.Ordinal), actualPaths);
        foreach (var entry in target.Entries)
        {
            using var expected = target.OpenFile(entry.Path);
            using var actual = fileSystem.Open("DATA/" + entry.Path);
            using var expectedBytes = new MemoryStream();
            using var actualBytes = new MemoryStream();
            expected.CopyTo(expectedBytes);
            actual.CopyTo(actualBytes);
            Assert.Equal(expectedBytes.ToArray(), actualBytes.ToArray());
        }
        Assert.False(fileSystem.FileExists("remove/nested.ini"));
    }

    [Fact]
    public void CompactWritesTheEffectiveOverlayTreeWithoutTombstones()
    {
        using var fixture = new Fixture();
        var basePath = fixture.PackDirectory("base", new Dictionary<string, string>
        {
            ["ships/base.ini"] = "base",
            ["ships/change.ini"] = "old",
            ["remove.ini"] = "remove"
        });
        var overlaySource = fixture.CreateSource("overlay", new Dictionary<string, string>
        {
            ["ships/change.ini"] = "new",
            ["ships/add.ini"] = "added"
        });
        var targetPath = fixture.PackDirectory("target", new Dictionary<string, string>
        {
            ["ships/base.ini"] = "base",
            ["ships/change.ini"] = "new",
            ["ships/add.ini"] = "added"
        }, 8);
        var overlayPath = Path.Combine(fixture.Root, "overlay.nap");
        NapWriter.Pack(
        [
            .. Directory.EnumerateFiles(overlaySource, "*", SearchOption.AllDirectories)
                .Select(path => new NapSourceEntry(
                    Path.GetRelativePath(overlaySource, path).Replace(Path.DirectorySeparatorChar, '/'), path)),
            new NapSourceEntry("remove.ini", null)
        ], overlayPath);
        var baseArchive = NapArchive.Open(basePath);
        var overlayArchive = NapArchive.Open(overlayPath);
        var provider = new NexusPackageFileProvider(
        [
            new NexusPackageMount("base", baseArchive, 1, 1, new HashSet<string>(StringComparer.Ordinal)),
            new NexusPackageMount("patch", overlayArchive, 2, 1, new HashSet<string>(["base"], StringComparer.Ordinal))
        ]);
        var compactPath = Path.Combine(fixture.Root, "compact.nap");

        NapOverlayBuilder.Compact(provider, compactPath, 8);
        var compact = NapArchive.Open(compactPath);
        compact.VerifyAll();

        Assert.Equal(8UL, compact.ContentVersion);
        Assert.Equal(new[] { "ships/add.ini", "ships/base.ini", "ships/change.ini" },
            compact.Entries.Select(x => x.Path).Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(compact.Entries, x => x.Tombstone);
        var target = NapArchive.Open(targetPath);
        Assert.Equal(target.Entries.Select(x => x.Path).Order(StringComparer.Ordinal),
            compact.Entries.Select(x => x.Path).Order(StringComparer.Ordinal));
        foreach (var entry in target.Entries)
        {
            using var expected = target.OpenFile(entry.Path);
            using var actual = compact.OpenFile(entry.Path);
            using var expectedBytes = new MemoryStream();
            using var actualBytes = new MemoryStream();
            expected.CopyTo(expectedBytes);
            actual.CopyTo(actualBytes);
            Assert.Equal(expectedBytes.ToArray(), actualBytes.ToArray());
        }
        using var changed = compact.OpenFile("ships/change.ini");
        using var reader = new StreamReader(changed);
        Assert.Equal("new", reader.ReadToEnd());
    }

    [Fact]
    public void CompactExportsEditorWorkspaceOverridesAndNewFiles()
    {
        using var fixture = new Fixture();
        var basePath = fixture.PackDirectory("workspace-base", new Dictionary<string, string>
        {
            ["ships/change.ini"] = "packaged"
        });
        var archive = NapArchive.Open(basePath);
        var packageProvider = new NexusPackageFileProvider(
        [
            new NexusPackageMount("base", archive, 1, 1, new HashSet<string>(StringComparer.Ordinal))
        ]);
        var workspaceDirectory = Path.Combine(fixture.Root, "workspace");
        var workspace = new NexusPackageWorkspaceFileProvider(workspaceDirectory, packageProvider);
        var changedPath = workspace.GetBackingFileName("DATA/ships/change.ini", out var destination);
        Assert.True(changedPath);
        File.WriteAllText(destination!, "edited");
        var newPath = Path.Combine(workspaceDirectory, "ships", "added.ini");
        File.WriteAllText(newPath, "new editor asset");
        var fileSystem = new FileSystem(packageProvider, workspace);
        var compactPath = Path.Combine(fixture.Root, "workspace-export.nap");

        NapOverlayBuilder.Compact(fileSystem, compactPath, 3);
        var compact = NapArchive.Open(compactPath);
        compact.VerifyAll();
        Assert.Equal(new[] { "ships/added.ini", "ships/change.ini" },
            compact.Entries.Select(x => x.Path).Order(StringComparer.Ordinal).ToArray());
        using var stream = compact.OpenFile("ships/change.ini");
        using var reader = new StreamReader(stream);
        Assert.Equal("edited", reader.ReadToEnd());
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "nap-overlay-tests-" + Guid.NewGuid().ToString("N"));

        public Fixture() => Directory.CreateDirectory(Root);

        public string CreateSource(string name, IReadOnlyDictionary<string, string> files)
        {
            var root = Path.Combine(Root, name);
            foreach (var pair in files)
            {
                var path = Path.Combine(root, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, pair.Value);
            }
            return root;
        }

        public string PackDirectory(string name, IReadOnlyDictionary<string, string> files, ulong version = 1)
        {
            var source = CreateSource(name, files);
            var output = Path.Combine(Root, name + ".nap");
            NapWriter.PackDirectory(source, output, new NapPackOptions { ContentVersion = version });
            return output;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private static IEnumerable<string> EnumerateFiles(FileSystem fileSystem, string directory)
    {
        foreach (var file in fileSystem.GetFiles(directory))
            yield return directory.Length == 0 ? file : directory + "/" + file;
        foreach (var child in fileSystem.GetDirectories(directory))
        {
            var childPath = directory.Length == 0 ? child : directory + "/" + child;
            foreach (var file in EnumerateFiles(fileSystem, childPath))
                yield return file;
        }
    }
}
