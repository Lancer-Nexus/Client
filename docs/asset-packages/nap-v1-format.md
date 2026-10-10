# NAP V1 Binary Format

This document freezes the binary layout implemented by `Nexus.Assets` format version 1. All integer fields are unsigned, fixed-width, little-endian. UTF-8 decoding is strict. V1 does not execute archive contents.

## Header: 128 bytes

| Offset | Size | Field |
|---:|---:|---|
| 0 | 8 | Magic bytes `4c 4e 41 50 00 0d 0a 1a` (`LNAP\0 CR LF SUB`) |
| 8 | 2 | Header version, exactly 1 |
| 10 | 2 | Header size, exactly 128 |
| 12 | 4 | Flags, zero in V1 |
| 16 | 16 | First 16 bytes of the deterministic package identifier hash |
| 32 | 8 | Content version |
| 40 | 4 | Chunk size hint, 1 through 1,048,576 |
| 44 | 4 | Reserved, zero |
| 48 | 8 | Index offset |
| 56 | 8 | Index length |
| 64 | 8 | Chunk-table offset |
| 72 | 8 | Chunk-table length |
| 80 | 8 | Payload offset |
| 88 | 8 | Payload length |
| 96 | 32 | SHA-256 of the complete index bytes |

Sections are contiguous in this order: header, index, chunk table, payload. The reader rejects overlap, out-of-file ranges and payload trailing/short bytes.

## Index

Starts with 8 bytes `NAPIDX1\0`, then a u32 entry count. Entries are sorted by normalized UTF-8 path using ordinal ordering; paths must also be unique under ordinal case-insensitive comparison.

Each entry is: u16 path byte length; path bytes; u8 flags (`0` file, `1` tombstone); u64 uncompressed file length; 32-byte SHA-256 of uncompressed file bytes; u32 chunk-reference count; that many u32 chunk-table indices. Empty regular files have length and reference count zero and SHA-256 of the empty byte string. Tombstones have zero length, zero hash bytes and zero chunk references.

Virtual paths use `/`, are relative, contain no empty, `.` or `..` components, NUL, colon, Windows-invalid characters, trailing dot/space, or reserved Windows device names. `DATA/` and `EXE/../DATA/` prefixes are canonicalized away. Component limit is 255 UTF-16 code units; encoded path limit is 4096 bytes. UTF-8 must be strictly valid.

## Chunk table and payload

Each fixed 64-byte chunk record is: 32-byte SHA-256 of uncompressed bytes; u64 stored length; u64 uncompressed length; u64 absolute payload offset; u8 codec; 7 reserved zero bytes. Codec `0` is stored bytes and requires equal stored/uncompressed lengths. Codec `1` is one independent Zstandard frame. Chunk lengths are 1 through 1 MiB; stored chunk length is capped at 1 MiB + 64 KiB. V1 has no dictionaries.

The writer chunks each source file in order, deduplicates identical chunks by SHA-256 and length within the package, and emits no payload for empty files or tombstones. The package identifier is the first 16 bytes of SHA-256 over each sorted entry's UTF-8 path concatenated with its 32-byte content hash. The reader validates chunk bounds, codec, reserved bytes, non-overlap, decompressed length and chunk hash. `verify` also validates the complete uncompressed file hash.

## Golden vector

With content version 1, chunk size 16, compression level 3, and one file `a.bin` containing byte `7f`, the complete deterministic NAP file SHA-256 is:

```text
6b2865ad4e2ed41aada33af2045b331cd85b16e8bda8c40271b3931c63aa0be1
```

The vector is asserted by `Nexus.Assets.Tests.NapArchiveTests.MatchesVersionOneGoldenVector`. Any incompatible binary layout change requires a new major format version.
