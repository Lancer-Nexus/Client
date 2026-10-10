using System.Security.Cryptography;
using System.Buffers.Binary;
using Nexus.Assets;
using Xunit;

namespace Nexus.Assets.Tests;

public sealed class NapArchiveTests
{
    [Fact]
    public void RoundTripsEmptySmallAndChunkedFilesWithSeekAndConcurrentStreams()
    {
        using var fixture = new Fixture();
        fixture.Write("empty.bin", []);
        fixture.Write(@"DATA\TEXT\hello.txt", "hello NAP"u8.ToArray());
        var random = new byte[47_321];
        RandomNumberGenerator.Fill(random);
        fixture.Write("large/random.bin", random);
        var repeated = Enumerable.Repeat((byte)0x41, 32_000).ToArray();
        fixture.Write("large/repeated.bin", repeated);

        var archivePath = fixture.Package(chunkSize: 4096);
        var archive = NapArchive.Open(archivePath, chunkCacheBytes: 4096);
        archive.VerifyAll();
        Assert.Equal(4, archive.Entries.Count);
        Assert.Equal("hello NAP", ReadText(archive.OpenFile("exe/../data/text/HELLO.txt")));
        using (var input = archive.OpenFile("large/random.bin"))
        {
            input.Seek(19_000, SeekOrigin.Begin);
            var sample = new byte[7000];
            Assert.Equal(sample.Length, input.Read(sample));
            Assert.Equal(random.AsSpan(19_000, sample.Length).ToArray(), sample);

            input.Seek(3000, SeekOrigin.Begin);
            Span<byte> spanSample = stackalloc byte[6000];
            Assert.Equal(spanSample.Length, input.Read(spanSample));
            Assert.Equal(random.AsSpan(3000, spanSample.Length).ToArray(), spanSample.ToArray());
        }

        Parallel.For(0, 12, _ =>
        {
            using var input = archive.OpenFile("large/repeated.bin");
            input.Seek(12_345, SeekOrigin.Begin);
            var sample = new byte[10_000];
            Assert.Equal(sample.Length, input.Read(sample));
            Assert.All(sample, b => Assert.Equal((byte)0x41, b));
        });
        Assert.InRange(archive.CachedChunkBytes, 0, 4096);
    }

    [Fact]
    public void PackingIdenticalInputIsDeterministic()
    {
        using var fixture = new Fixture();
        fixture.Write("a.txt", "same content"u8.ToArray());
        var first = fixture.Package("first.nap");
        var second = fixture.Package("second.nap");
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
    }

    [Fact]
    public void MatchesVersionOneGoldenVector()
    {
        using var fixture = new Fixture();
        fixture.Write("a.bin", [0x7f]);
        var path = fixture.Package("golden.nap", 16);
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        Assert.Equal("6b2865ad4e2ed41aada33af2045b331cd85b16e8bda8c40271b3931c63aa0be1", actual);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("C:/device")]
    [InlineData("a//b")]
    [InlineData("a/./b")]
    [InlineData("CON.txt")]
    [InlineData("a/COM1.bin")]
    [InlineData("trailing.")]
    [InlineData("bad?.txt")]
    public void RejectsUnsafeVirtualPaths(string path) => Assert.Throws<InvalidDataException>(() => NapPath.Normalize(path));

    [Fact]
    public void RejectsCorruptHeaderAndPayload()
    {
        using var fixture = new Fixture();
        fixture.Write("data.bin", Enumerable.Repeat((byte)7, 12000).ToArray());
        var package = fixture.Package(chunkSize: 4096);
        var bytes = File.ReadAllBytes(package);
        bytes[0] ^= 0xff;
        var badHeader = fixture.PathFor("bad-header.nap");
        File.WriteAllBytes(badHeader, bytes);
        Assert.Throws<InvalidDataException>(() => NapArchive.Open(badHeader));

        bytes = File.ReadAllBytes(package);
        bytes[^1] ^= 0x55;
        var badPayload = fixture.PathFor("bad-payload.nap");
        File.WriteAllBytes(badPayload, bytes);
        Assert.Throws<InvalidDataException>(() => NapArchive.Open(badPayload).VerifyAll());
    }

    [Fact]
    public void RejectsCaseInsensitiveDuplicatePaths()
    {
        using var fixture = new Fixture();
        fixture.Write("Data/file.ini", [1]);
        fixture.Write("data/FILE.ini", [2]);
        Assert.Throws<InvalidDataException>(() => fixture.Package());
    }

    [Fact]
    public void WritesAndReadsTombstoneEntries()
    {
        using var fixture = new Fixture();
        fixture.Write("keep.txt", [1, 2, 3]);
        var tombstonePath = fixture.PathFor("deleted-from-base.txt");
        NapWriter.Pack(
        [
            new NapSourceEntry("keep.txt", Path.Combine(fixture.Source, "keep.txt")),
            new NapSourceEntry("DATA/deleted-from-base.txt", null)
        ], tombstonePath);
        var archive = NapArchive.Open(tombstonePath);
        Assert.True(archive.TryGetEntry("deleted-from-base.txt", out var tombstone));
        Assert.True(tombstone!.Tombstone);
        Assert.Throws<FileNotFoundException>(() => archive.OpenFile("DATA/deleted-from-base.txt"));
    }

    [Fact]
    public void ExtractsVerifiedFilesIntoNewDirectoryAndOmitsTombstones()
    {
        using var fixture = new Fixture();
        fixture.Write("nested/hello.txt", "hello from NAP"u8.ToArray());
        var packagePath = fixture.PathFor("editable-package.nap");
        NapWriter.Pack(
        [
            new NapSourceEntry("nested/hello.txt", Path.Combine(fixture.Source, "nested", "hello.txt")),
            new NapSourceEntry("removed.ini", null)
        ], packagePath);
        var unpackParent = fixture.PathFor("unpacked");
        Directory.CreateDirectory(unpackParent);

        var destination = NapArchive.Open(packagePath).ExtractToDirectory(unpackParent);

        Assert.Equal(Path.Combine(unpackParent, "editable-package"), destination);
        Assert.Equal("hello from NAP", File.ReadAllText(Path.Combine(destination, "nested", "hello.txt")));
        Assert.False(File.Exists(Path.Combine(destination, "removed.ini")));
    }

    [Fact]
    public void EditorPackAndUnpackPreservesCompleteDirectoryTree()
    {
        using var fixture = new Fixture();
        var sourceDirectory = Path.Combine(fixture.Source, "DATA");
        Directory.CreateDirectory(sourceDirectory);
        var baseIni = Path.Combine(sourceDirectory, "systems", "li01", "base.ini");
        var texture = Path.Combine(sourceDirectory, "textures", "ship.dds");
        Directory.CreateDirectory(Path.GetDirectoryName(baseIni)!);
        Directory.CreateDirectory(Path.GetDirectoryName(texture)!);
        File.WriteAllText(baseIni, "[Base]\nSystem = LI01\n");
        File.WriteAllBytes(texture, RandomNumberGenerator.GetBytes(131_077));
        File.WriteAllBytes(Path.Combine(sourceDirectory, "empty.dat"), []);
        var packagePath = fixture.PathFor("editor-export.nap");
        var outputParent = fixture.PathFor("unpacked");
        Directory.CreateDirectory(outputParent);

        NapWriter.PackDirectory(sourceDirectory, packagePath);
        var destination = NapArchive.Open(packagePath).ExtractToDirectory(outputParent);

        var sourceFiles = Directory.GetFiles(fixture.Source, "*", SearchOption.AllDirectories)
            .Where(path => path.StartsWith(sourceDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(path => (SourcePath: path,
                RelativePath: Path.GetRelativePath(sourceDirectory, path)))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var extractedFiles = Directory.GetFiles(destination, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(destination, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(sourceFiles.Select(file => file.RelativePath), extractedFiles);
        foreach (var file in sourceFiles)
            Assert.Equal(File.ReadAllBytes(file.SourcePath), File.ReadAllBytes(Path.Combine(destination,
                file.RelativePath)));
    }

    [Fact]
    public void ExtractionRejectsExistingDestinationWithoutOverwritingIt()
    {
        using var fixture = new Fixture();
        fixture.Write("hello.txt", "package content"u8.ToArray());
        var archive = NapArchive.Open(fixture.Package("existing.nap"));
        var parent = fixture.PathFor("unpacked");
        var destination = Path.Combine(parent, "existing");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "keep.txt"), "keep");

        Assert.Throws<IOException>(() => archive.ExtractToDirectory(parent));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(destination, "keep.txt")));
    }

    [Fact]
    public void ExtractionRejectsPayloadCorruptionWithoutLeavingPartialOutput()
    {
        using var fixture = new Fixture();
        fixture.Write("nested/first.bin", RandomNumberGenerator.GetBytes(8192));
        fixture.Write("nested/second.bin", RandomNumberGenerator.GetBytes(8192));
        var packagePath = fixture.Package("corrupted-after-open.nap", chunkSize: 4096);
        var archive = NapArchive.Open(packagePath);
        var packageBytes = File.ReadAllBytes(packagePath);
        var payloadOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(packageBytes.AsSpan(80, 8)));
        packageBytes[payloadOffset] ^= 0xff;
        File.WriteAllBytes(packagePath, packageBytes);
        var parent = fixture.PathFor("unpacked");
        Directory.CreateDirectory(parent);

        Assert.Throws<InvalidDataException>(() => archive.ExtractToDirectory(parent));
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    private static string ReadText(Stream stream)
    {
        using (stream)
        using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "nap-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string source;
        public Fixture()
        {
            source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
        }
        public void Write(string path, byte[] content)
        {
            var fullPath = Path.Combine(source, path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, content);
        }
        public string Package(string file = "out.nap", int chunkSize = 256 * 1024)
        {
            var output = PathFor(file);
            NapWriter.PackDirectory(source, output, new NapPackOptions { ChunkSize = chunkSize });
            return output;
        }
        public string PathFor(string name) => Path.Combine(root, name);
        public string Source => source;
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
