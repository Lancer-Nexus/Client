# NAP audio pipeline audit

Audit date: 2026-10-09. This records the current Client decoder and runtime paths before any NAP audio conversion or alias implementation. No proprietary game audio was used; runtime decoder checks used generated one-second sine samples.

## Codec compatibility

| Input | Decoder path | Current evidence | NAP suitability |
|---|---|---|---|
| PCM WAV, mono/stereo, 8/16-bit | `extern/lancerdecode/src/formats/riff.c` | Synthetic WAV decoded by the native decoder smoke | Safe compatibility baseline; eligible for optional per-chunk Zstd |
| MP3 file | `src/formats/mp3.c` using bundled `dr_mp3.h` | Synthetic MP3 decoded to signed 16-bit PCM | Runtime decode exists; lossy re-encoding is not a default conversion |
| MP3 in RIFF/WAV | `riff.c` dispatches WAVE format `0x55` to `mp3_getstream`; `fact` and Freelancer `trim` chunks control sample trimming | Synthetic MP3-in-WAV decoded; codec/container properties reported as `mp3` / `wav` | Preserve the existing WAV wrapper and trim metadata for compatible legacy assets |
| FLAC | `src/formats/flac.c` using bundled `dr_flac.h` | Synthetic FLAC decoded to signed 16-bit PCM | Decoder is in the native build; streaming is technically available through the common decoder interface |
| Ogg Vorbis | `src/formats/vorbis.c` using bundled `stb_vorbis.c` | Synthetic Ogg/Vorbis decoded to signed 16-bit PCM | Decoder is in the native build; candidate for evaluation after runtime loop and package tests |
| Ogg FLAC | `src/autoload.c` dispatches the Ogg FLAC identification packet to `flac_getstream(..., isOgg: 1)` | Code-path evidence only; no synthetic runtime check in this audit | Treat as supported by source, not yet qualified for shipping |
| Ogg Opus | `src/formats/opus.c` dynamically calls `libopusfile` | Synthetic Opus failed in the current x64 Linux smoke: `libopusfile_Open()` could not load the selected 32-bit `/usr/lib/libopusfile.so.0.4.2` into the 64-bit process | Not a dependable target until matching per-platform runtime libraries are packaged and verified. Windows code expects `libopusfile-0.dll`; the Client build/staging tree has no matching copy rule |

The native smoke built `extern/lancerdecode` with its supplied CMake example and generated WAV, MP3, FLAC, and Ogg/Vorbis samples using FFmpeg. WAV, MP3, MP3-in-WAV, FLAC, and Ogg/Vorbis each decoded successfully. The Opus failure is an observed host/runtime dependency mismatch, not evidence that the Opus source path is absent. No Windows decoder execution was available.

## Runtime data path

1. `GameDataManager.GetAudioStream()` finds the `AudioEntry.File`, checks it through `Items.VFS`, and returns `Items.VFS.Open(...)`. `SoundManager.LoadGenericSoundAsync()` passes that stream to `SoundData.LoadStream()` for ordinary effects; `SoundManager.PlayMusic()` passes the stream to `MusicPlayer.Play()`.
2. `SoundLoader.Open()` constructs `AudioDecoder`, which calls the native `lancerdecode` decoder. The native decoder consumes managed read/seek/tell callbacks; compressed formats produce PCM incrementally. `NapEntryStream` is seekable and reads across chunk boundaries, so it can back the decoder without extracting the encoded file. Its span-read path now copies directly from cached chunks without allocating a temporary buffer for every decoder refill; `NapArchiveTests` covers a span read crossing multiple chunks.
3. Music uses `StreamingSource`: it queues 8 initial OpenAL buffers of 8192 bytes, then refills processed buffers on the audio-manager update path. `MusicPlayer` owns 24 buffers. One-shot sound effects instead use `SoundData.LoadStream()` and copy the complete decoded PCM into a native allocation. Voice lines are read from UTF data into memory and then decoded into `SoundData`; they are not streamed from NAP.
4. `AudioDecoder` advertises `CanSeek == false` and only implements a reset to offset zero through its public `Seek()`. The native codec callbacks also reset to the start only. `StreamingSource` implements whole-track looping by seeking to zero; there is no per-asset loop-start/end metadata or gapless-loop contract in this runtime path.

## Existing editor conversion

`src/Editor/LibreLancer.ContentEdit/AudioImporter.cs` uses the same native `AudioDecoder`. It classifies PCM WAV and selected MP3/WAV trim cases for passthrough; other recognized decoder inputs fall through to `NeedsConversion`. `src/Editor/LancerEdit/Audio/AudioConverter.cs` converts those to Freelancer-compatible MP3-in-WAV through `Mp3Encoder`, or copies compatible WAV data. This is an editor import workflow, not an NAP package builder or runtime alias system. The LAME encoder is a separate native dependency (`libmp3lame`); Linux mapping exists in `src/PublishAssets/LibreLancer.ContentEdit.dll.config`.

## Alias and packaging boundary

The current runtime resolves only the path stored in `AudioEntry.File`. NAP mounts provide normal VFS reads but no audio-alias resolution. The `audioAliases` example in the NAP architecture document has no corresponding signed manifest, active-snapshot, or runtime implementation. Any future alias must resolve only inside the audio-loading path; changing general `IFileProvider.Open("*.wav")` behavior could return non-WAV bytes to unrelated readers.

An alias design must retain the original WAV fallback, bind the alias and codec requirement to the signed active package snapshot, validate the target path and digest through the existing package validation, and reject aliases when the target decoder is unavailable. Long music may be evaluated for streaming codecs; converting one-shot SFX or voice data does not reduce runtime decoded-memory use in the current path.

## Phase 6 entry gates and implementation order

The NAP VFS/updater still has open Windows CI/game-runtime and interactive editor verification, so Phase 6 conversion is not ready to ship. When that base is complete:

1. Add platform-specific `lancerdecode` and Opus dependency staging checks; fail packaging when a declared decoder is unavailable. Exercise decoder loading on Windows and Linux.
2. Add a synthetic C# decoder test harness that reads WAV, MP3, FLAC, Vorbis and Opus streams from both loose files and NAP-backed `NapEntryStream`; include short-read behavior, boundary-crossing chunks, reset/loop reads, corrupt/truncated streams, mono/stereo and long-stream memory checks.
3. Extend the signed release and active snapshot with a bounded, validated audio-alias table. Resolve aliases in `GameDataManager.GetAudioStream()` (or a dedicated audio resolver) while preserving the original path fallback.
4. Add opt-in `nexus-pack audio analyze`, `convert`, and `verify` commands with explicit per-asset profiles, reproducible tool/version reporting, file hashes, channel/rate preservation, and PCM fallback. Do not make MP3 transcoding the default for already-lossy inputs.
5. Validate loop boundaries, gapless behavior, SFX start latency, voice timing, repeated playback and AB listening comparisons in the real Client on both platforms. Until this passes, keep loop-sensitive and short SFX as WAV/PCM.

No decoder or audio-alias behavior was changed by this audit. The package format can already store encoded audio bytes; that fact alone does not establish codec availability, streaming behavior, loop quality, or legal distribution rights.
