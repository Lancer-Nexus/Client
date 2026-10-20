# NAP audio pipeline audit

Audit date: 2026-10-09. This records the current Client decoder and runtime paths before any NAP audio conversion or alias implementation. No proprietary game audio was used; runtime decoder checks used generated one-second sine samples.

## Codec compatibility

| Input | Decoder path | Current evidence | NAP suitability |
|---|---|---|---|
| PCM WAV, mono/stereo, 8/16-bit | `extern/lancerdecode/src/formats/riff.c` | Synthetic WAV decoded by the native decoder smoke | Safe compatibility baseline; eligible for optional per-chunk Zstd |
| MP3 file | `src/formats/mp3.c` using bundled `dr_mp3.h` | Synthetic MP3 decoded to signed 16-bit PCM | Runtime decode exists; lossy re-encoding is not a default conversion |
| MP3 in RIFF/WAV | `riff.c` dispatches WAVE format `0x55` to `mp3_getstream`; `fact` and Freelancer `trim` chunks control sample trimming | Synthetic MP3-in-WAV decoded; codec/container properties reported as `mp3` / `wav` | Preserve the existing WAV wrapper and trim metadata for compatible legacy assets |
| FLAC | `src/formats/flac.c` using bundled `dr_flac.h` | Synthetic FLAC decoded to signed 16-bit PCM; managed stream-reset regression passes with 8192-byte reads; active NAP Client music streaming confirmed on Linux | Native decoder and active-package music paths work on Linux; Windows runtime remains open |
| Ogg Vorbis | `src/formats/vorbis.c` using bundled `stb_vorbis.c` | Synthetic Ogg/Vorbis decoded to signed 16-bit PCM; managed EOF/reset regression passes after patch 2172; active NAP Client playback, single-track loop and A/B playlist confirmed on Linux | Native and Client music/SFX loop paths work on Linux; acoustic loop-boundary quality and Windows runtime remain open |
| Ogg FLAC | `src/autoload.c` dispatches the Ogg FLAC identification packet to `flac_getstream(..., isOgg: 1)` | Synthetic Ogg FLAC decoded to signed 16-bit PCM; managed EOF/reset and OpenAL loop checks pass; active NAP Client music streaming confirmed on Linux | Native decoder and active-package music paths work on Linux; Windows DLL is staged, but Windows runtime remains open |
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
5. Repeated Ogg/Vorbis music and SFX playback is verified on Linux (see the follow-up below). Still validate loop-boundary quality, gapless behavior, SFX start latency, voice timing and AB listening in the real Client on both platforms. Keep loop-sensitive and short SFX as WAV/PCM until those quality checks pass.

The original audit did not change decoder or audio-alias behavior. The package format can already store encoded audio bytes; that fact alone does not establish codec availability, streaming behavior, loop quality, or legal distribution rights.

## Follow-up: Vorbis/FLAC playback and loops (2026-10-12)

**Ergebnis zur Vorbis-Schleife:** Ja. Ogg/Vorbis-Musik wird im Client bei
aktiviertem Loop nach EOF ab Streamanfang weitergespielt; die Regression und
der OpenAL-Streaminglauf bestätigen den Neustart. Wiederholte Vorbis-SFX
funktionieren ebenfalls, weil SFX vorab zu PCM dekodiert und anschließend
über `AL_LOOPING` wiederholt werden. Belegt ist die Wiederholung, nicht ein
nahtloser oder artefaktfreier Übergang: Loop-Punkte innerhalb der Datei und
ein Hörtest an der Schleifengrenze fehlen weiterhin.

**Codepfad bestätigt:** `MusicPlayer.Play(..., loop: true)` reicht den Loop-
Schalter an `StreamingSource.Begin()` weiter. Beim Erreichen des Streamendes
setzt `StreamingSource` den `AudioDecoder` auf Offset 0 zurück und füllt die
OpenAL-Puffer erneut. Patch 2172 behandelt dabei den Vorbis-spezifischen
Decoderreset, indem der Decoder über denselben seekbaren Eingabestream neu
geöffnet wird. `AudioDecoder.CanSeek` bleibt nach außen `false`; unterstützt
wird ausschließlich der interne Reset auf den Dateianfang.

Für SFX decodiert `SoundData.LoadStream()` die Datei vollständig nach PCM;
`SoundInstance.Play(loop: true)` setzt danach `AL_LOOPING` an der OpenAL-Quelle.
Vorbis-SFX hängen beim Wiederholen somit nicht von einem erneuten Vorbis-Seek
ab. Die geprüft unterstützte Semantik ist in beiden Fällen die Wiederholung
der ganzen Datei. Vorbis-Kommentar-Tags oder sonstige Loop-Start-/Endpunkte
werden in diesem Pfad nicht ausgewertet.

**Windows-Prüfung:** Der Client-Workflow ist für den nativen Bau von
`lancerdecode.dll`, das Kopieren in den Testausgabeordner und
`AudioDecoderLoopTests` eingerichtet. GitHub Actions ist für dieses Repository
derzeit deaktiviert; daher liegt noch kein erfolgreicher Windows-CI-Lauf vor.
Windows-Loader und Decoderlaufzeit bleiben unbestätigt.

- `GameDataManager.GetAudioStream()` resolves the file named by `AudioEntry`
  from the VFS and passes the stream to `SoundManager`; decoding is selected by
  file signature, so `.ogg`/`.flac` are usable without an extension table or
  audio alias.
- Music is streamed through `StreamingSource` in 8192-byte reads. One-shot
  sounds instead decode completely into PCM; their repeats use OpenAL's
  source-loop flag and do not seek the compressed decoder during playback.
- Looping music reads the compressed stream again from offset zero when a
  streaming buffer reaches EOF. A one-track room uses that loop mode; a
  multi-track room plays each selected track once and advances after playback
  stops. The Ogg/Vorbis reset regression confirms that the decoder can restart
  after EOF. This verifies the reset prerequisite, not gapless audio at the
  loop boundary.
- A managed `AudioDecoder` run against the previous native build found that
  Vorbis decoded correctly to EOF but `stb_vorbis_seek(..., 0)` did not restart
  after EOF. Patch 2172 reopens the Vorbis decoder over the same seekable VFS
  stream on reset. Failed reopen is guarded and returns EOF instead of
  dereferencing a missing decoder.
- Ogg FLAC initially failed despite native container dispatch. The managed
  `AudioDecoder` seek callback returned the stream's new absolute position,
  while the native stream contract expects zero on success. Ogg FLAC needs a
  relative seek while parsing its mapping header, exposing the mismatch. The
  callback now returns zero after a successful seek; the synthetic Ogg FLAC
  decoder and reset regression passes with the same stream callback used by
  Client audio.
- Regression tests use the existing repository Ogg bell and a generated
  synthetic FLAC and Ogg FLAC fixture. Each is decoded to EOF, rewound, decoded
  again with the music buffer size, and compared byte-for-byte. The loose-file
  cases pass 3/3; the same three formats pass from chunked NAP entries, bringing
  the focused suite to 6/6 after rebuilding the managed decoder project.
- A Vorbis failure-path regression now blocks rewind after decoding to EOF.
  Reopening the decoder then fails, and a subsequent read returns EOF without
  dereferencing the missing native decoder. Repeated reset attempts are also
  covered: the native path skips closing an already absent decoder, remains at
  EOF, and does not crash. This verifies the guarded decoder error path; it
  does not establish audible music recovery after a failed loop.
- A fresh focused Linux rerun on 2026-10-12 passed `AudioDecoderLoopTests`
  (7/7) and `RoomMusicPlaylistTests` (5/5). This reconfirms decoder replay from
  loose and chunked NAP streams and the playlist parsing/selection rules. It
  does not establish audible loop-boundary quality or Windows runtime behavior.
- A temporary runtime harness used a synthetic 0.7-second Ogg/Vorbis track and
  OpenAL Soft's null driver. With looping enabled, `MusicPlayer` stayed in the
  playing state after 5 seconds; the non-looping control reached stopped after
  3 seconds.
  The same positive/negative control through `SoundData` and
  `SoundInstance.Play(loop)` kept the Vorbis SFX active after 3 seconds only
  when looping was enabled. This verifies real decoder, stream/source and
  OpenAL queue behavior without audible output; it does not prove loop-boundary
  quality.
- The same backend harness used synthetic Ogg FLAC: `MusicPlayer` remained
  playing after 5 seconds with looping enabled and stopped after 3 seconds
  without looping. An SFX loaded from Ogg FLAC, decoded to PCM and played with
  `SoundInstance.Play(loop: true)` stayed active after 3 seconds. Its non-loop
  control ended. This
  verifies Ogg FLAC music and SFX loop paths under OpenAL Soft's null driver,
  without audible output or loop-boundary quality measurements.
- A focused Client regression test packs each synthetic Vorbis, FLAC and Ogg
  FLAC track into NAP with 1024-byte chunks, opens it as a `NapEntryStream`,
  decodes to EOF and resets to compare the PCM. All three cases pass, covering
  repeated seek/read across chunk boundaries. This does not cover mounting an
  active Client package snapshot or a no-loose-`DATA` launch by itself.
- Focused native/managed decoder checks passed on Linux. An initial disposable
  Client package opened track A but did not advance. Its assemblies were stale:
  replacing only the Client and Media DLLs caused a missing
  `BaseRoom.MusicTracks` field at runtime. After synchronizing the complete
  Debug output into the disposable package, a 20-second test-save run logged
  stream terminal states and repeatedly selected A/B in alternation; `strace`
  confirmed repeated opens of both synthetic Ogg files. A separate 16.67-second
  PulseAudio monitor capture from the Client had 44.1-kHz stereo PCM and a
  measured -24 dBFS peak while the test tracks played. The capture was routed
  to a temporary PulseAudio null sink, so it confirms non-silent Client/OpenAL
  output without sending test tones to speakers. That earlier loose-data run
  did not establish active-package mounting or perceptual loop-boundary quality.
- A separate 22-second Debug Client run mounted a verified active snapshot with
  the retail DATA tree packed as `freelancer-data.nap` and a second NAP overriding
  the BR01 Deck room plus two synthetic Vorbis tracks. The install had no loose
  `DATA` directory. Logs reached `Game: Finished loading game data` and
  `UI: Interface loaded`, then selected both NAP-backed tracks 20 times in
  alternation. A 21.41-second PulseAudio monitor capture routed to a temporary
  null sink contained 44.1-kHz stereo PCM (mean -34.2 dBFS, peak -7.5 dBFS);
  0.5-second FFT windows during playback showed the generated 440-Hz and
  660-Hz track tones in turn.
  This confirms active-snapshot VFS reads and non-silent playback during the
  track sequence on Linux; it does not prove audible loop-boundary quality or
  active-package FLAC/Ogg FLAC playback.
- A further 18-second Debug Client run used the same active NAP snapshot with
  only the 0.7-second, 440-Hz Vorbis track assigned to the BR01 Deck room.
  The Client logged one selection with `oneshot=False` and no streaming terminal
  state. The 17.41-second 44.1-kHz stereo Null-Sink capture kept a 440-Hz
  spectral peak in every 0.5-second window from second 7 through second 15.
  This verifies repeated MusicPlayer playback after EOF through the active NAP
  stream on Linux. It does not assess audible clicks or seamlessness at the
  loop boundary.
- An active-NAP Debug Client run also put synthetic FLAC and Ogg FLAC in the
  same two-track BR01 Deck playlist. Both entries were selected repeatedly,
  each stream reached EOF, and playback advanced between formats. Its
  16.41-second 44.1-kHz stereo Null-Sink capture had a -10.6 dBFS peak; 0.2-
  second FFT windows showed the generated ~393-Hz FLAC and ~525-Hz Ogg FLAC
  tones in turn. This confirms both streaming decoder paths from the active
  snapshot on Linux, without claiming acoustic quality or Windows support.
- A separate temporary probe loaded synthetic Vorbis, FLAC and Ogg FLAC SFX
  through `NexusPackageFileProvider.LoadActive()` and `SoundData.LoadStream()`,
  then played each with the Client `AudioManager` on OpenAL Soft/PulseAudio.
  All three looped instances remained active after 2.5 seconds; each non-looping
  control stopped after 1.4 seconds, including the 1.0-second Ogg FLAC sample.
  The 18.83-second 44.1-kHz stereo Null-Sink capture had a -28.6 dBFS mean and
  -22.4 dBFS peak, with the generated 440-Hz Vorbis, ~392-Hz FLAC and ~524-Hz
  Ogg FLAC tones visible during their runs. This proves NAP-backed decode and
  SFX loop playback at the decoder/AudioManager layer on Linux; it is not a
  full game-scene SFX trigger test or an acoustic boundary assessment.
- `BuildNatives --with-win64` and `BuildEngine --with-win64` succeeded through
  the actual BuildLL path. The final `bin/librelancer-win-x64/lib/x64/`
  directory contains `lancerdecode.dll`, byte-identical to the x64 native stage
  in `bin/natives/x64/`. It is x86-64 PE32+ and imports only `KERNEL32.dll`
  and `msvcrt.dll`. Wine is unavailable here, so Windows loader resolution and
  fixture decoding remain unverified.
  Ordinary FLAC, Ogg/Vorbis and Ogg FLAC are not blocked on a new alias or
  format resolver.

## Code-Abgleich: Musik- und SFX-Loops (2026-10-12)

Der Quellcode bestätigt den oben beschriebenen Loop-Support für Ogg/Vorbis:
`extern/lancerdecode/src/formats/vorbis.c` implementiert `ogg_seek()` für den
Reset auf Byte 0, indem der Vorbisdecoder geschlossen und auf demselben
Seek-fähigen Stream neu geöffnet wird. `StreamingSource` setzt den Decoder bei
EOF auf den Anfang zurück und füllt die OpenAL-Musikpuffer erneut.
`SoundManager.PlayMusic()` aktiviert diesen Modus standardmäßig
(`oneshot = false`). Ein Raum mit genau einem Track behält dieses Loop-Verhalten,
sofern `MusicOneShot` nicht gesetzt ist. Bei mehreren Tracks wird jeder Track
einmal abgespielt und die Playlist wählt anschließend den nächsten aus.

SFX folgen einem eigenen Weg: `SoundData.LoadStream()` dekodiert den Sound
zunächst vollständig nach PCM. Wiederholung ist optional und wird vom Aufrufer
über `SoundInstance.Play(loop: true)` angefordert; `AudioManager` setzt dafür
`AL_LOOPING`. Der Decoder muss während der Wiedergabe nicht zurückgesetzt
werden. Ohne dieses Flag bleibt der SFX ein One-Shot.

Damit ist wiederholte Vorbis-Musik- und SFX-Wiedergabe auf Linux durch
Decoderregressionen und OpenAL-/Clientläufe belegt. Die Prüfung belegt keinen
nahtlosen Übergang oder frei definierbare Loop-Punkte. Der Vorbis-Reset-
Regressionstest liegt unter `src/LibreLancer.Tests/AudioDecoderLoopTests.cs`;
Windows-Laufzeit und ein Hörtest der Loop-Grenze bleiben offen.

Der fokussierte Regressionstest wurde am 2026-10-12 erneut ausgeführt:
`AudioDecoderLoopTests` bestand mit 7/7 Fällen: drei Decoder-Resets (Vorbis,
FLAC und Ogg FLAC) aus losen Dateien, drei Wiederholungen aus chunked
NAP-Einträgen und ein Vorbis-Reopen-Fehlerfall. `RoomMusicPlaylistTests`
bestand im selben Lauf mit 5/5 Fällen; zusammen waren es 12/12 Tests. Die
Vorbis-Fixture ergab nach Reset byteidentisches PCM; ein blockierter Rewind
führt kontrolliert zu EOF statt zu einem Absturz. Der Testlauf bestätigt
Decoder-Reset und reproduzierbare PCM-Daten; Musik- und SFX-Loop-Wiedergabe
ist zusätzlich durch die oben beschriebenen OpenAL- und Clientläufe belegt.
Reproduzierbarer Aufruf:
`DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 dotnet test
src/LibreLancer.Tests/LibreLancer.Tests.csproj --no-restore
-p:UseSharedCompilation=false --filter
'FullyQualifiedName~AudioDecoderLoopTests|FullyQualifiedName~RoomMusicPlaylistTests'`.

Die vollständige `LibreLancer.Tests`-Suite wurde am selben Tag in Release
erneut ausgeführt und bestand mit 677/677 Tests. Das ist ein breiterer
verwalteter Client-Testlauf; Windows-Loader, native Windows-Decodierung und
akustische Loop-Grenzen bleiben davon unbestätigt.

Der fokussierte Lauf wurde anschließend unter Linux ohne erneuten Build mit
`--no-build --no-restore` wiederholt: `AudioDecoderLoopTests` und
`RoomMusicPlaylistTests` bestanden erneut zusammen mit 12/12 Fällen. Das
bestätigt die zuvor dokumentierte Decoder- und Playlistabdeckung; den OpenAL-
Loop-Nachweis liefern weiterhin die separaten Backend- und Clientläufe oben.

Ein erneuter fokussierter Linux-Lauf am 2026-10-12 bestand mit `AudioDecoderLoopTests` 7/7. Der Aufruf war:
`DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 /opt/dotnet-10/dotnet test src/LibreLancer.Tests/LibreLancer.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter FullyQualifiedName~AudioDecoderLoopTests`.
Damit ist der aktuelle Decoder-Reset-Test bestätigt. Der erste Versuch ohne `DOTNET_ROOT` konnte wegen des nicht gefundenen .NET-10-Runtimes nicht bauen; der Lauf mit explizitem Runtimepfad war erfolgreich.

Ein weiterer fokussierter Lauf am 2026-10-12 baute die aktuelle Debug-Testassembly
und bestand `AudioDecoderLoopTests` sowie `RoomMusicPlaylistTests` zusammen mit
12/12 Fällen. Verwendet wurde der oben dokumentierte .NET-10-Aufruf mit dem
Filter `FullyQualifiedName~AudioDecoderLoopTests|FullyQualifiedName~RoomMusicPlaylistTests`.
Damit ist der Decoder-Reset samt Playlistregel erneut bestätigt; der separate
OpenAL-/Clientnachweis belegt tatsächliches Wiederholen, aber keine nahtlose
Schleifengrenze.
