# NAP Code Audit

Auditstand: 2026-10-09. Quellen wurden im aktuellen Client-Checkout geprüft; der Arbeitsbaum enthält umfangreiche unabhängige lokale Änderungen. Diese wurden nicht bereinigt oder überschrieben.

## VFS

- `src/LibreLancer.Data/IO/FileSystem.cs`: `IFileProvider` hat `Open`, `FileExists`, `GetBackingFileName`, `GetFiles`, `GetDirectories` und `Refresh`. Dateiöffnung und Existenzprüfung fragen Provider rückwärts ab, daher gewinnt der zuletzt gemountete Provider.
- `FileSystem.GetFiles` und `GetDirectories` führen Provider-Ergebnisse mit case-insensitive HashSets zusammen. Die Methoden kennen keine Tombstones und entfernen keine maskierten älteren Einträge. Korrekte Sichtbarkeit maskierter Dateien benötigt einen effektiven Index oder eine gezielte VFS-Erweiterung.
- `patches/2070-package-and-mount-data-overlays.patch` ist in `patches/series` eingetragen. `FreelancerDataOverlayFileProvider` mappt DATA-Pfade auf ein Verzeichnis. `GameConfig.CreateFreelancerFileSystem()` bindet `lib/data` ein; `LLServer.Core.ServerApp` bindet sein Serveroverlay ein. Das ist Verzeichnisfallback, kein Containerreader.
- Phase 2 adds `IOverlayFileProvider` interception to `FileSystem` and `NexusPackageFileProvider`. Client startup (`GameConfig.CreateFreelancerFileSystem`) and LLServer startup load `packages/active.json`; absent snapshots keep the previous loose-data path. The mount checks package path confinement, reparse points, size and whole-package SHA-256 before loading the NAP index. The separate Updater validates NAP V1 section bounds, index/path/reference structure, decompressed chunk hashes, complete file hashes and signed content-version agreement before publishing the staged active snapshot.

## Physische Pfadconsumer

`GetBackingFileName()` wird in folgenden Gruppen benutzt:

- **Editor-Mount:** `src/Editor/LancerEdit/GameDataContext.cs::Load` erstellt `FileSystem.FromPath(folder)` und ergänzt danach `NexusPackageFileProvider.LoadActive(AppContext.BaseDirectory)` sowie einen persistenten Workspace-Provider. LancerEdit liest aktive Pakete aus demselben Client-Releaseverzeichnis wie der Spielclient.
- **Lesen mit physischem Pfad:** `src/LibreLancer.Data/Schema/FreelancerIni.cs` übergibt den Pfad an `ResourceDll.FromStream`. `ResourceDll.SavePath` wird anschließend von `src/Editor/LibreLancer.ContentEdit/EditableInfocardManager.cs::Save` für `File.Create` verwendet. Das ist damit zugleich ein Schreibpfad und kein rein lesender DLL-Import.
- **Datei-Saves:** `StarSystemSaveStrategy`, `UniverseSaveStrategy`, `NewsSaveStrategy`, `BaseNpcSaveStrategy` und `TradingPlannerTab.Markets` schreiben INI-Dateien direkt über den zurückgegebenen OS-Pfad. `MissionScriptDocument` lädt und schreibt referenzierte NPC-Schiffdateien ebenfalls über physische Pfade.
- **Verzeichnis-/Mehrdatei-Saves:** `UniverseEditorTab.NewSystem` benötigt einen physischen Systems-Ordner, legt Unterverzeichnisse und neue INIs direkt an und aktualisiert danach die Universe-Datei.
- **Interface-Editor:** `src/Editor/InterfaceEdit/ResourceWindow.cs` schreibt `resources.xml` mit `File.WriteAllText` an den Backing-Pfad.

`InterfaceEdit/Project.cs::Load` erstellt aktuell nur `FileSystem.FromPath(FlFolder)` und lädt weder den aktiven Client-Paketsnapshot noch den persistenten NAP-Workspace. Damit ist der direkte `resources.xml`-Schreibpfad derzeit ein reiner Loose-Data-Pfad und kein NAP-Workspace-Save. Die NAP-Pack-/Entpack-Integration ist in LancerEdit umgesetzt; InterfaceEdit bleibt als separates Editor-Integrationspaket offen.

Package-backed saves resolve to the persistent workspace above package and loose DATA providers. The LancerEdit Data menu now offers NAP extraction, directory packing, and export of the active package snapshot plus workspace edits. Extraction verifies every entry before writing into a staging directory and moves the completed directory into the selected parent; tombstones are omitted. Packing and export run off the UI thread and report completion or errors through editor dialogs. Export produces a verified full NAP but does not sign or activate a release. DLL resource editing uses the workspace path stored in `ResourceDll.SavePath`. These new interactive flows and Windows behavior still need runtime verification.

Package-backed INI saves in `NewsSaveStrategy`, `UniverseSaveStrategy`, `StarSystemSaveStrategy`, `AsteroidFieldList`, `UniverseEditorTab`, and `MissionScriptDocument` now resolve through `WritableDataPath`. A missing physical target raises an explicit `IOException`, and mission NPC references are no longer silently discarded when no writable path can be resolved. `BaseNpcSaveStrategy` and commodity market saves retain their existing explicit failure handling.

## Updater und Gateway

- `src/LibreLancer/Net/NexusGatewayLogin.cs` lädt `client-version.json`, sendet `ClientVersionHello` an `/api/v1/client/version` und blockiert Login bei Update-/Protokollinkompatibilität. Der Clientvertrag enthält `DataManifestId` und Gateway-Antwort `RequiredDataManifestId` im Protocol-Submodule.
- `README.md` dokumentiert gemeinsame Client-/NAP-Aktivierung; ein Linux-Start mit vollständigem NAP-Container und ohne loses `DATA` erreichte am 2026-10-09 das erste UI. Der Updater weist NAP-Strukturfehler vor Aktivierung zurück und serialisiert Installationsläufe über einen exklusiven Lock im Installationsstamm. Updater-Tests decken den fehlenden UI-Health-Ack-Timeout einschließlich Prozessbeendigung und Rückkehr zum vorherigen Release ab. Native Kindprozess-Tests beenden den Updater abrupt nach dem Verschieben des Releases, nach dem Flush des temporären Pointers und nach dem atomaren Pointerwechsel. Der aktive Snapshot bleibt jeweils vollständig; nach einem Tempfile-Abbruch gelingt auch der nächste Aktivierungslauf. Der Windows-CI-Lauf ist mangels erreichbarer GitHub-API noch nicht bestätigt. Windows-Spielruntime bleibt unvalidiert. Login-Acknowledgements verwenden Exitcode 42, fehlende/defekte Clientmetadaten Exitcode 43.
- `src/Editor/LancerEdit/Updater/UpdateChecks.cs` und `UpdateDownloader.cs` behandeln Editorupdates. Sie sind kein installierter Spielcontent-Updater und sollten nicht ohne gesonderte Prüfung als NAP-Installationspfad verwendet werden.

## Build und Tests

- Zielprojekte verwenden .NET 10. `ZstdSharp.Port` wird bereits in `src/LibreLancer.Base/LibreLancer.Base.csproj` verwendet.
- Die NAP-V1-Projekte sind isoliert als `src/Nexus.Assets`, `src/Nexus.Packaging.Cli` und `src/Nexus.Assets.Tests` angelegt; Tests laufen unabhängig vom bestehenden, lokal veränderten Gesamttestprojekt.
- Phase 1 bindet keine Original-Freelancer-Dateien ein. Tests verwenden synthetische Bytes.

## Folgeabhängigkeiten

LancerEdit mounts the active Client package snapshot and routes package-backed physical save paths to a persistent workspace. The workspace can be compacted with package layers into a new verified full NAP, but signing and activation remain explicit publisher steps. Interactive editor saves and Windows behavior are unverified. The separate Updater repository verifies signed NAP package metadata and the complete NAP V1 structure/content, downloads required/selected packages with dependency and override closure, writes `packages/active.json` in the Client staging release, and activates Client plus data through one release pointer. The Client acknowledges readiness after its first UI state loads; the Updater can restore the previous release and records failed package identity to prevent an activation loop. A full DATA image in one NAP passed a Linux launch with no loose DATA and reached the first UI on 2026-10-09. Linux tests cover a missing-ack timeout, process termination, old-release restoration and failed-snapshot suppression. Windows runtime and real process interruption between activation steps remain open. Gateway-/Protocol-Verträge must decide whether `DataManifestId` identifies the whole release or a gameplay-critical subset.

The Updater resumes hash-addressed downloads using validated byte ranges and retains partial data after transport interruption. It checks available volume space before package download, client extraction and data-package copies. Real disk-full injection and Windows filesystem behavior still need verification.

## Audio pipeline

The native decoder supports PCM WAV, MP3, FLAC and Ogg/Vorbis in source and synthetic Linux smoke checks. Ogg/Opus source exists but depends on a dynamically loaded platform library; the current x64 environment resolves a 32-bit `libopusfile` and cannot load Opus. `GameDataManager.GetAudioStream()` reads only the path stored in `AudioEntry.File`; no audio-alias layer exists. See [`audio-pipeline.md`](audio-pipeline.md) for codec evidence, stream/loop behavior and Phase 6 gates.

`src/Nexus.Assets/NapOverlayBuilder.cs` builds `.nap` overlays from two verified archives and compacts an active package mount. The CLI diff sidecar describes the one base package dependency/override relationship; publishers must review and merge it into their signed manifest.
