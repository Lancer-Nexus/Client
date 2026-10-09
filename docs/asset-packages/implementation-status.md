# Nexus Asset Packages Implementation Status

Status updated 2026-10-09 after the full-data NAP-only Linux game smoke test and local Updater CI-equivalent checks.

| Area | State | Evidence / remaining work |
|---|---|---|
| Code audit | implemented | `code-audit.md`; current VFS, overlay, backing-path consumers, Gateway version API and updater boundaries inventoried |
| NAP V1 header/index/chunks | implemented | `src/Nexus.Assets`; frozen layout in `nap-v1-format.md`, golden vector test |
| Reader / seek stream | implemented | Bounds checks, strict paths, per-chunk SHA-256, Zstd expansion limit, configurable bounded LRU cache |
| Writer / deterministic output | implemented | Directory and entry-based writer, chunk deduplication, `none`/Zstd selection, atomic temporary output, tombstones |
| CLI | implemented | `pack`, `inspect`, `verify`, `diff` and `compact` in `src/Nexus.Packaging.Cli` |
| Unit tests | implemented | Synthetic assets, roundtrip, seeks, concurrent streams, determinism, golden vector, tombstones, unsafe paths, corruption rejection before extraction with no partial output, verified NAP extraction, no-overwrite destination handling, VFS fallback/overrides/enumeration, active snapshot validation, and diff-to-mounted-target tree/byte parity; 28/28 tests pass locally on Linux; the test project also builds for win-x64 locally (7 projects, 0 warnings/errors), with Windows test execution still pending the configured CI matrix |
| VFS provider and unified enumeration | implemented | `NexusPackageFileProvider` mounts local active packages; `IOverlayFileProvider` masks lower providers for reads/existence/backing paths and filters file/directory enumeration; Client and LLServer attach it |
| LancerEdit workspace and package tools | partial | LancerEdit mounts the active Client package snapshot above selected loose DATA and writes package-backed saves to a persistent workspace. The Data menu can unpack a verified NAP into a new folder, pack a selected folder as NAP, and export the active package snapshot with workspace edits. An Editor-path integration test selects a synthetic DATA folder, packs it, unpacks the NAP, and verifies the full relative file tree and bytes (including an empty file); 28/28 NAP asset tests pass. On 2026-10-09, a Linux LancerEdit GUI roundtrip used the Data menu and native dialogs to pack and unpack a synthetic folder; all three relative paths and file contents matched byte-for-byte, and CLI verification passed. Patch 2106 defers chained file dialogs to the next UI update, and patch 2113 keeps SDL filter buffers alive through the asynchronous callback and disposes synchronous NFD filter buffers deterministically. Windows behavior remains to verify |
| Active package snapshot validation | partial | Client/LLServer reject unsafe paths, reparse points, size/hash mismatch and malformed NAP indexes; Updater verifies NAP V1 structure, content version and all chunk/file hashes before writing the staged snapshot, then rechecks package hashes before reuse |
| Download resume and storage checks | partial | `ArtifactDownloader` resumes digest-scoped `.part` files with validated HTTP Range responses and exclusive per-digest locking; download, TAR extraction and NAP staging check available volume space. Interrupted-response and mocked insufficient-space tests pass; real ENOSPC and cross-platform filesystem behavior remain unverified |
| `GetBackingFileName` consumers | partial | Runtime package entries return no OS backing path. LancerEdit routes package-backed save paths through a checked writable-path helper into its persistent workspace; missing paths now fail clearly instead of being passed to INI writers as null. Commodity and MBases saves retain explicit status handling. InterfaceEdit and other OS-specific consumers plus interactive save flows still need runtime verification |
| Signed release manifest / updater | partial | `Updater/Manifest.cs` verifies canonical Ed25519-signed NAP metadata; `DataPackageStager` selects required/optional dependency closure, validates NAP V1 structure/content and writes `packages/active.json`; staging rejects undeclared cross-package path conflicts. Client and data activate together through an atomic release pointer; startup health acknowledgement and one-generation rollback are implemented. Full game DATA in one NAP reached the Linux first UI state with loose `DATA` absent. Updater tests pass 35/35; [CI run 37939599307](https://github.com/Lancer-Nexus/Updater/actions/runs/37939599307) passed on Ubuntu and Windows. Download's [CI run 37942797584](https://github.com/Lancer-Nexus/Download/actions/runs/37942797584) passes the signed client `tar.zst` plus NAP download, verification, combined snapshot, and activation flow (4/4 tests). Windows game/editor runtime, real ENOSPC, and production release publication with provisioned trust root remain unverified |
| Overlay diff/compaction | partial | `NapOverlayBuilder` builds content-hash diffs with tombstones and compacts active package layers. A synthetic end-to-end test mounts the generated diff on its baseline and verifies the complete virtual path set and bytes against the target archive. `diff` writes an advisory dependency/override plan. Multi-package manifest authoring remains manual; compaction does not include loose DATA fallback |
| NAPD | planned | Optional after complete V1 overlay flow |
| Audio pipeline | planned | Codec and stream-path audit is complete in `audio-pipeline.md`; synthetic native Linux checks pass for PCM WAV, MP3, MP3-in-WAV, FLAC and Vorbis. Opus cannot load the host's 32-bit `libopusfile` into the x64 decoder process. Windows dependency staging, NAP-backed decoder tests, real loop/playback checks and Windows VFS/updater runtime verification remain before Phase 6 implementation |

## Build and test commands

```bash
dotnet test src/Nexus.Assets.Tests/Nexus.Assets.Tests.csproj -p:UseSharedCompilation=false
dotnet build src/Nexus.Packaging.Cli/Nexus.Packaging.Cli.csproj -p:UseSharedCompilation=false
dotnet run --project src/Nexus.Packaging.Cli/Nexus.Packaging.Cli.csproj -- pack ./DATA ./packages/local.nap
dotnet run --project src/Nexus.Packaging.Cli/Nexus.Packaging.Cli.csproj -- verify ./packages/local.nap
```

The `DATA` packaging example is local-only. Do not distribute proprietary Freelancer data without the required rights.
