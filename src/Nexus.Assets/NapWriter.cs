using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ZstdSharp;

namespace Nexus.Assets;

public sealed record NapPackOptions
{
    public ulong ContentVersion { get; init; } = 1;
    public int ChunkSize { get; init; } = 256 * 1024;
    public int CompressionLevel { get; init; } = 3;
}

public sealed record NapSourceEntry(string Path, string? FileName);

public static class NapWriter
{
    private sealed record PendingEntry(string Path, long Length, byte[] Hash, int[] Chunks, bool Tombstone);
    private sealed record PendingChunk(byte[] Hash, byte[] Stored, long Length, byte Codec);

    public static void PackDirectory(string sourceDirectory, string outputFile, NapPackOptions? options = null)
    {
        var root = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("NAP source directory cannot be a symbolic link or reparse point.");
        var output = Path.GetFullPath(outputFile);
        var relativeOutput = Path.GetRelativePath(root, output);
        if (!Path.IsPathRooted(relativeOutput) && relativeOutput != ".." &&
            !relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("NAP output must be outside the source directory.");
        var files = EnumerateSourceFiles(root);
        Pack(files, output, options);
    }

    private static IEnumerable<NapSourceEntry> EnumerateSourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Symbolic links and reparse points are not packable: {subdirectory}");
                pending.Push(subdirectory);
            }
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Symbolic links and reparse points are not packable: {file}");
                var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                yield return new NapSourceEntry(NapPath.Normalize(relative), file);
            }
        }
    }

    public static void Pack(IEnumerable<NapSourceEntry> sourceEntries, string outputFile, NapPackOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourceEntries);
        options ??= new NapPackOptions();
        if (options.ChunkSize is < 1 or > NapLimits.MaxChunkSize)
            throw new ArgumentOutOfRangeException(nameof(options), "Chunk size must be between 1 byte and 1 MiB.");
        if (options.CompressionLevel is < 1 or > 22)
            throw new ArgumentOutOfRangeException(nameof(options), "Zstandard level must be between 1 and 22.");
        var files = sourceEntries
            .Select(x => (Path: NapPath.Normalize(x.Path), File: x.FileName is null ? null : Path.GetFullPath(x.FileName)))
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .ToArray();
        if (files.Length > NapLimits.MaxEntries) throw new InvalidDataException("Source has too many files.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in files)
            if (!seen.Add(item.Path)) throw new InvalidDataException($"Case-insensitive duplicate path: {item.Path}");

        var chunks = new List<PendingChunk>();
        var chunkByHash = new Dictionary<string, int>(StringComparer.Ordinal);
        var entries = new List<PendingEntry>(files.Length);
        foreach (var item in files)
        {
            if (item.File is null)
            {
                entries.Add(new PendingEntry(item.Path, 0, new byte[32], [], true));
                continue;
            }
            if ((File.GetAttributes(item.File) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Symbolic links and reparse points are not packable: {item.File}");
            using var input = File.OpenRead(item.File);
            using var fileHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var refs = new List<int>();
            var buffer = new byte[options.ChunkSize];
            long fileLength = 0;
            while (true)
            {
                var length = ReadChunk(input, buffer);
                if (length == 0) break;
                var raw = buffer.AsSpan(0, length).ToArray();
                fileHash.AppendData(raw);
                fileLength = checked(fileLength + length);
                var digest = SHA256.HashData(raw);
                var key = Convert.ToHexString(digest) + ":" + length;
                if (!chunkByHash.TryGetValue(key, out var chunkIndex))
                {
                    var stored = raw;
                    byte codec = 0;
                    using var compressed = new MemoryStream();
                    using (var encoder = new CompressionStream(compressed, options.CompressionLevel, 0, true))
                        encoder.Write(raw);
                    if (compressed.Length < raw.Length)
                    {
                        stored = compressed.ToArray();
                        codec = 1;
                    }
                    chunkIndex = chunks.Count;
                    chunks.Add(new PendingChunk(digest, stored, length, codec));
                    chunkByHash.Add(key, chunkIndex);
                }
                refs.Add(chunkIndex);
            }
            entries.Add(new PendingEntry(item.Path, fileLength, fileHash.GetHashAndReset(), refs.ToArray(), false));
        }

        if (chunks.Count > NapLimits.MaxChunks) throw new InvalidDataException("Source has too many chunks.");
        var index = BuildIndex(entries);
        if (index.Length > NapLimits.MaxIndexLength) throw new InvalidDataException("NAP index exceeds size limit.");
        var chunkTableLength = checked((long)chunks.Count * NapLimits.ChunkRecordLength);
        var indexOffset = (long)NapLimits.HeaderLength;
        var tableOffset = checked(indexOffset + index.Length);
        var payloadOffset = checked(tableOffset + chunkTableLength);
        var offsets = new long[chunks.Count];
        var payloadLength = 0L;
        for (var i = 0; i < chunks.Count; i++)
        {
            offsets[i] = checked(payloadOffset + payloadLength);
            payloadLength = checked(payloadLength + chunks[i].Stored.Length);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputFile))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(outputFile) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            {
                var header = BuildHeader(entries, chunks, options, index, indexOffset, tableOffset,
                    chunkTableLength, payloadOffset, payloadLength);
                writer.Write(header);
                writer.Write(index);
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    writer.Write(chunk.Hash);
                    writer.Write((ulong)chunk.Stored.Length);
                    writer.Write((ulong)chunk.Length);
                    writer.Write((ulong)offsets[i]);
                    writer.Write(chunk.Codec);
                    writer.Write(new byte[7]);
                }
                foreach (var chunk in chunks) writer.Write(chunk.Stored);
                writer.Flush();
                output.Flush(true);
            }
            File.Move(temporary, outputFile, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    private static int ReadChunk(Stream input, byte[] buffer)
    {
        var used = 0;
        while (used < buffer.Length)
        {
            var read = input.Read(buffer, used, buffer.Length - used);
            if (read == 0) break;
            used += read;
        }
        return used;
    }

    private static byte[] BuildIndex(List<PendingEntry> entries)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("NAPIDX1\0"));
        writer.Write((uint)entries.Count);
        foreach (var entry in entries)
        {
            var path = Encoding.UTF8.GetBytes(entry.Path);
            if (path.Length > NapLimits.MaxPathBytes) throw new InvalidDataException("NAP path is too long.");
            writer.Write((ushort)path.Length);
            writer.Write(path);
            writer.Write((byte)(entry.Tombstone ? 1 : 0));
            writer.Write((ulong)entry.Length);
            writer.Write(entry.Hash);
            writer.Write((uint)entry.Chunks.Length);
            foreach (var chunk in entry.Chunks) writer.Write((uint)chunk);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildHeader(List<PendingEntry> entries, List<PendingChunk> chunks,
        NapPackOptions options, byte[] index, long indexOffset, long tableOffset,
        long tableLength, long payloadOffset, long payloadLength)
    {
        using var identity = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in entries)
        {
            identity.AppendData(Encoding.UTF8.GetBytes(entry.Path));
            identity.AppendData(entry.Hash);
        }
        var digest = identity.GetHashAndReset();
        var header = new byte[NapLimits.HeaderLength];
        new byte[] { 0x4c, 0x4e, 0x41, 0x50, 0x00, 0x0d, 0x0a, 0x1a }.CopyTo(header, 0);
        PutU16(header, 8, 1);
        PutU16(header, 10, NapLimits.HeaderLength);
        digest.AsSpan(0, 16).CopyTo(header.AsSpan(16, 16));
        PutU64(header, 32, options.ContentVersion);
        PutU32(header, 40, (uint)options.ChunkSize);
        PutU64(header, 48, (ulong)indexOffset);
        PutU64(header, 56, (ulong)index.Length);
        PutU64(header, 64, (ulong)tableOffset);
        PutU64(header, 72, (ulong)tableLength);
        PutU64(header, 80, (ulong)payloadOffset);
        PutU64(header, 88, (ulong)payloadLength);
        SHA256.HashData(index).CopyTo(header, 96);
        return header;
    }

    private static void PutU16(byte[] target, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset, 2), value);
    private static void PutU32(byte[] target, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset, 4), value);
    private static void PutU64(byte[] target, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(target.AsSpan(offset, 8), value);
}
