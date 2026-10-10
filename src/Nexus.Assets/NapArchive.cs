using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ZstdSharp;

namespace Nexus.Assets;

public sealed record NapEntry(string Path, long Length, byte[] Sha256, IReadOnlyList<int> Chunks, bool Tombstone);

internal sealed record NapChunk(byte[] Sha256, long CompressedLength, long Length, long Offset, byte Codec);

public sealed class NapArchive
{
    private static readonly byte[] Magic = [0x4c, 0x4e, 0x41, 0x50, 0x00, 0x0d, 0x0a, 0x1a];
    private readonly string fileName;
    private readonly NapChunk[] chunks;
    private readonly Dictionary<string, NapEntry> entries;
    private readonly NapChunkCache cache;

    public Guid PackageId { get; }
    public ulong ContentVersion { get; }
    public IReadOnlyCollection<NapEntry> Entries => entries.Values;
    public long CachedChunkBytes => cache.Size;

    private NapArchive(string fileName, Guid packageId, ulong contentVersion,
        NapChunk[] chunks, Dictionary<string, NapEntry> entries, int cacheBudget)
    {
        this.fileName = fileName;
        PackageId = packageId;
        ContentVersion = contentVersion;
        this.chunks = chunks;
        this.entries = entries;
        cache = new NapChunkCache(cacheBudget);
    }

    public static NapArchive Open(string fileName, int chunkCacheBytes = 32 * 1024 * 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (chunkCacheBytes < 0) throw new ArgumentOutOfRangeException(nameof(chunkCacheBytes));
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Open(stream, fileName, chunkCacheBytes);
    }

    internal static NapArchive Open(Stream stream, string fileName, int chunkCacheBytes = 32 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!stream.CanRead || !stream.CanSeek) throw new ArgumentException("NAP stream must be readable and seekable.", nameof(stream));
        if (chunkCacheBytes < 0) throw new ArgumentOutOfRangeException(nameof(chunkCacheBytes));
        stream.Position = 0;
        if (stream.Length < NapLimits.HeaderLength)
            throw new InvalidDataException("NAP header is truncated.");
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var header = reader.ReadBytes(NapLimits.HeaderLength);
        if (!header.AsSpan(0, 8).SequenceEqual(Magic)) throw new InvalidDataException("Invalid NAP magic.");
        if (U16(header, 8) != 1 || U16(header, 10) != NapLimits.HeaderLength)
            throw new InvalidDataException("Unsupported NAP header version or size.");
        if (U32(header, 12) != 0 || U32(header, 44) != 0)
            throw new InvalidDataException("NAP contains unknown flags or nonzero reserved bytes.");

        var packageId = new Guid(header.AsSpan(16, 16));
        var version = U64(header, 32);
        var indexOffset = CheckedLong(U64(header, 48));
        var indexLength = CheckedLong(U64(header, 56));
        var chunkOffset = CheckedLong(U64(header, 64));
        var chunkLength = CheckedLong(U64(header, 72));
        var payloadOffset = CheckedLong(U64(header, 80));
        var payloadLength = CheckedLong(U64(header, 88));
        var chunkHint = U32(header, 40);
        if (chunkHint is 0 or > NapLimits.MaxChunkSize || indexLength is < 8 or > NapLimits.MaxIndexLength ||
            chunkLength % NapLimits.ChunkRecordLength != 0 ||
            chunkLength / NapLimits.ChunkRecordLength > NapLimits.MaxChunks)
            throw new InvalidDataException("NAP table sizes exceed supported limits.");
        Range(indexOffset, indexLength, stream.Length);
        Range(chunkOffset, chunkLength, stream.Length);
        Range(payloadOffset, payloadLength, stream.Length);
        if (indexOffset < NapLimits.HeaderLength || chunkOffset < indexOffset + indexLength ||
            payloadOffset < chunkOffset + chunkLength || payloadOffset + payloadLength != stream.Length)
            throw new InvalidDataException("NAP sections overlap or do not cover the declared package layout.");

        stream.Position = indexOffset;
        var indexBytes = reader.ReadBytes((int)indexLength);
        if (indexBytes.Length != indexLength || !SHA256.HashData(indexBytes).AsSpan().SequenceEqual(header.AsSpan(96, 32)))
            throw new InvalidDataException("NAP index hash mismatch.");

        stream.Position = chunkOffset;
        var chunkCount = checked((int)(chunkLength / NapLimits.ChunkRecordLength));
        var chunks = new NapChunk[chunkCount];
        var occupied = new List<(long Start, long End)>(chunkCount);
        for (var i = 0; i < chunkCount; i++)
        {
            var record = reader.ReadBytes(NapLimits.ChunkRecordLength);
            var codec = record[56];
            if (codec > 1 || record.AsSpan(57, 7).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("NAP chunk has an unsupported codec or nonzero reserved bytes.");
            var compressedLength = CheckedLong(U64(record, 32));
            var length = CheckedLong(U64(record, 40));
            var offset = CheckedLong(U64(record, 48));
            if (length is <= 0 or > NapLimits.MaxChunkSize || compressedLength is <= 0 or > NapLimits.MaxCompressedChunkSize ||
                offset < payloadOffset || compressedLength > payloadOffset + payloadLength - offset ||
                (codec == 0 && compressedLength != length))
                throw new InvalidDataException("NAP chunk bounds or lengths are invalid.");
            chunks[i] = new NapChunk(record[..32], compressedLength, length, offset, codec);
            occupied.Add((offset, checked(offset + compressedLength)));
        }
        occupied.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 1; i < occupied.Count; i++)
            if (occupied[i].Start < occupied[i - 1].End)
                throw new InvalidDataException("NAP chunk payloads overlap.");

        var entries = ParseIndex(indexBytes, chunks);
        return new NapArchive(Path.GetFullPath(fileName), packageId, version, chunks, entries, chunkCacheBytes);
    }

    public bool TryGetEntry(string path, out NapEntry? entry) => entries.TryGetValue(NapPath.Normalize(path), out entry);

    public Stream OpenFile(string path)
    {
        var normalized = NapPath.Normalize(path);
        if (!entries.TryGetValue(normalized, out var entry) || entry.Tombstone)
            throw new FileNotFoundException("NAP entry does not exist.", normalized);
        return new NapEntryStream(fileName, entry, chunks, cache);
    }

    public void VerifyAll()
    {
        foreach (var entry in entries.Values)
        {
            if (entry.Tombstone) continue;
            using var input = OpenFile(entry.Path);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                hash.AppendData(buffer, 0, read);
            if (!hash.GetHashAndReset().AsSpan().SequenceEqual(entry.Sha256))
                throw new InvalidDataException($"NAP file hash mismatch: {entry.Path}");
        }
    }

    /// <summary>Verifies and extracts this archive into a new child directory.</summary>
    public string ExtractToDirectory(string parentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        VerifyAll();
        var parent = Path.GetFullPath(parentDirectory);
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
        var directoryName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(directoryName) || directoryName is "." or ".." ||
            !string.Equals(NapPath.Normalize(directoryName), directoryName, StringComparison.Ordinal))
            throw new InvalidDataException("NAP filename cannot be used as an extraction directory name.");
        var destination = Path.Combine(parent, directoryName);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"Unpack destination already exists: {destination}");

        var staging = Path.Combine(parent, ".nap-unpack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var entry in entries.Values)
            {
                if (entry.Tombstone) continue;
                var relative = NapPath.Normalize(entry.Path).Replace('/', Path.DirectorySeparatorChar);
                var outputPath = Path.GetFullPath(Path.Combine(staging, relative));
                var outputRelative = Path.GetRelativePath(staging, outputPath);
                if (Path.IsPathRooted(outputRelative) || outputRelative == ".." ||
                    outputRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidDataException($"NAP entry escapes extraction directory: {entry.Path}");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                using var input = OpenFile(entry.Path);
                using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
                output.Flush(flushToDisk: true);
            }
            Directory.Move(staging, destination);
            return destination;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static Dictionary<string, NapEntry> ParseIndex(byte[] data, NapChunk[] chunks)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (!reader.ReadBytes(8).AsSpan().SequenceEqual(Encoding.ASCII.GetBytes("NAPIDX1\0")))
            throw new InvalidDataException("Invalid NAP index magic.");
        var count = reader.ReadUInt32();
        if (count > NapLimits.MaxEntries) throw new InvalidDataException("NAP entry count exceeds limit.");
        var result = new Dictionary<string, NapEntry>(StringComparer.OrdinalIgnoreCase);
        string? previous = null;
        for (var i = 0; i < count; i++)
        {
            var pathLength = reader.ReadUInt16();
            if (pathLength is 0 or > NapLimits.MaxPathBytes) throw new InvalidDataException("Invalid NAP path length.");
            var rawPath = new UTF8Encoding(false, true).GetString(ReadExact(reader, pathLength));
            var path = NapPath.Normalize(rawPath);
            if (!string.Equals(path, rawPath, StringComparison.Ordinal) ||
                (previous is not null && StringComparer.Ordinal.Compare(previous, path) >= 0))
                throw new InvalidDataException("NAP paths are not canonical and strictly sorted.");
            previous = path;
            var flags = reader.ReadByte();
            if (flags > 1) throw new InvalidDataException("Unknown NAP entry flags.");
            var length = CheckedLong(reader.ReadUInt64());
            var hash = ReadExact(reader, 32);
            var refCount = reader.ReadUInt32();
            if (refCount > NapLimits.MaxChunks || (flags == 1 && (length != 0 || refCount != 0)))
                throw new InvalidDataException("Invalid NAP entry chunk list.");
            var refs = new int[checked((int)refCount)];
            long total = 0;
            for (var r = 0; r < refs.Length; r++)
            {
                refs[r] = checked((int)reader.ReadUInt32());
                if (refs[r] >= chunks.Length) throw new InvalidDataException("NAP entry references an unknown chunk.");
                total = checked(total + chunks[refs[r]].Length);
            }
            if (flags == 0 && total != length) throw new InvalidDataException("NAP entry length does not match its chunks.");
            if (flags == 0 && length == 0 && !hash.AsSpan().SequenceEqual(SHA256.HashData([])))
                throw new InvalidDataException("Empty NAP file has an invalid content hash.");
            if (flags == 1 && hash.AsSpan().IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("NAP tombstone has a nonzero content hash.");
            if (!result.TryAdd(path, new NapEntry(path, length, hash, refs, flags == 1)))
                throw new InvalidDataException("Duplicate NAP path.");
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing bytes in NAP index.");
        return result;
    }

    internal static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4));
    internal static ushort U16(byte[] b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o, 2));
    internal static ulong U64(byte[] b, int o) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o, 8));
    internal static long CheckedLong(ulong value) => value <= long.MaxValue ? (long)value : throw new InvalidDataException("NAP integer exceeds supported range.");
    internal static void Range(long offset, long length, long total)
    {
        if (offset < 0 || length < 0 || offset > total || length > total - offset)
            throw new InvalidDataException("NAP section is outside the package.");
    }
    private static byte[] ReadExact(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        return bytes.Length == count ? bytes : throw new InvalidDataException("NAP index is truncated.");
    }

    private sealed class NapChunkCache(int budget)
    {
        private readonly object gate = new();
        private readonly Dictionary<int, LinkedListNode<(int Index, byte[] Data)>> items = new();
        private readonly LinkedList<(int Index, byte[] Data)> lru = new();
        private long size;
        public long Size { get { lock (gate) return size; } }

        public byte[] Get(int index, Func<byte[]> load)
        {
            lock (gate)
            {
                if (items.TryGetValue(index, out var node))
                {
                    lru.Remove(node);
                    lru.AddFirst(node);
                    return node.Value.Data;
                }
                var data = load();
                if (data.Length <= budget)
                {
                    var added = lru.AddFirst((index, data));
                    items.Add(index, added);
                    size += data.Length;
                    while (size > budget && lru.Last is { } last)
                    {
                        lru.RemoveLast();
                        items.Remove(last.Value.Index);
                        size -= last.Value.Data.Length;
                    }
                }
                return data;
            }
        }
    }

    private sealed class NapEntryStream(string fileName, NapEntry entry, NapChunk[] chunks, NapChunkCache cache) : Stream
    {
        private readonly FileStream file = new(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        private byte[]? current;
        private int currentChunk = -1;
        private long position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => entry.Length;
        public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
        public override long Seek(long offset, SeekOrigin origin)
        {
            var next = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => checked(position + offset), SeekOrigin.End => checked(Length + offset), _ => throw new ArgumentOutOfRangeException(nameof(origin)) };
            if (next < 0) throw new IOException("Cannot seek before the beginning of a NAP file.");
            return position = next;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
            return ReadCore(buffer.AsSpan(offset, count));
        }
        public override int Read(Span<byte> buffer) => ReadCore(buffer);
        private int ReadCore(Span<byte> buffer)
        {
            var wanted = (int)Math.Min(buffer.Length, Math.Max(0, Length - position));
            var copied = 0;
            long baseOffset = 0;
            for (var i = 0; i < entry.Chunks.Count && copied < wanted; i++)
            {
                var chunk = chunks[entry.Chunks[i]];
                if (position >= baseOffset + chunk.Length) { baseOffset += chunk.Length; continue; }
                Load(i, chunk);
                var inChunk = checked((int)(position - baseOffset));
                var n = Math.Min(wanted - copied, current!.Length - inChunk);
                current.AsSpan(inChunk, n).CopyTo(buffer.Slice(copied, n));
                copied += n;
                position += n;
                baseOffset += chunk.Length;
            }
            return copied;
        }
        private void Load(int index, NapChunk chunk)
        {
            if (currentChunk == index) return;
            current = cache.Get(entry.Chunks[index], () =>
            {
                file.Position = chunk.Offset;
                var compressed = new byte[checked((int)chunk.CompressedLength)];
                file.ReadExactly(compressed);
                byte[] data;
                if (chunk.Codec == 0) data = compressed;
                else
                {
                    try
                    {
                        using var source = new MemoryStream(compressed, writable: false);
                        using var decoder = new DecompressionStream(source);
                        using var output = new MemoryStream(checked((int)chunk.Length));
                        var block = new byte[8192];
                        int read;
                        while ((read = decoder.Read(block, 0, block.Length)) != 0)
                        {
                            if (output.Length > chunk.Length - read)
                                throw new InvalidDataException("NAP chunk expands beyond its declared size.");
                            output.Write(block, 0, read);
                        }
                        data = output.ToArray();
                    }
                    catch (Exception exception) when (exception is not InvalidDataException)
                    {
                        throw new InvalidDataException("NAP chunk decompression failed.", exception);
                    }
                }
                if (data.LongLength != chunk.Length || !SHA256.HashData(data).AsSpan().SequenceEqual(chunk.Sha256))
                    throw new InvalidDataException("NAP chunk length or hash mismatch.");
                return data;
            });
            currentChunk = index;
        }
        protected override void Dispose(bool disposing) { if (disposing) file.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
