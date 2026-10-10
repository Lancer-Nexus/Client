# Agentenanweisung – Nexus Asset Packages implementieren

> Rolle: Senior C#/.NET Game Engine & Updater Engineer. Zielrepository: https://github.com/Lancer-Nexus/Client. Maßgebliche Architektur: `NEXUS-ASSET-PACKAGES.md` im selben Dokumentationspaket. Implementiere inkrementell, testgetrieben und ohne bestehende Standalone-Funktionen zu brechen.

## Auftrag

Führe das `.nap`-Paketformat als wahlfrei mountbares, komprimiertes, random-access-fähiges Dateisystem ein. Erlaube beliebig viele Container (Animationen, Sounds, Modelle, Texturen, Systeme, Missionen etc.), die unabhängig versioniert und über den Updater aktualisiert werden. Priorisiere Overlay-Patches; `.napd`-Binärdeltas erst nach funktionsfähigem V1. Integration in vorhandenes `IFileProvider` und das vorhandene Version-/Updateprotokoll.

## Unverzichtbare Arbeitsregeln

1. **Zuerst Repo-Audit**: `README.md`, `AGENTS.md`, `patches/series`, Patch 2070, `src/LibreLancer.Data/IO/FileSystem.cs`, `GameConfig`, `ServerApp`, `NexusGatewayLogin`, bestehende Launcher-/Updaterprojekte, CI und Tests untersuchen. Keine Behauptungen über existierende Funktionen ohne Codebeleg.
2. Architektur des Forks respektieren: Änderungen an Upstream-Dateien nach vorhandenen Overlay-/Patchkonventionen durchführen, keine blind direkten Änderungen, die beim nächsten Patch-Apply verloren gehen. Standalone-Modus erhalten.
3. Vor Implementierung **kurzen schriftlichen Auditbericht** mit echten Pfaden, Aufrufstellen, Konfliktrisiken, Migrationsstrategie und Abhängigkeiten erstellen.
4. Zuerst minimalen, verifizierbaren **NAP-V1-Slice** implementieren: Writer, Reader, Index, streamendes Lesen, SHA-256, Tests. Danach VFS/Updater in separaten Schritten.
5. Kein pauschales Entpacken aller Assets beim Spielstart. `GetBackingFileName()`-Callsites prüfen und gegebenenfalls eng begrenzten Materialisierungscache einführen.
6. Deterministische Paketpriorität; case-insensitive virtuelle Dateinamen; Tombstones, Verzeichnisenumeration und Overlay-Fallback korrekt behandeln. Nicht versuchen, Tombstones durch `Open() => null` zu realisieren.
7. Verschlüsselung/DRM außerhalb V1. Keine Original-Freelancer-Daten in Repo, Releases oder Testfixtures committen.
8. Download-/Archiv-/Manifestinhalte als nicht vertrauenswürdig behandeln. Sichere Bounds, Pfadvalidierung, SHA-256 und Ed25519-signiertes Manifest. Signaturen nicht durch Hashchecks ersetzen.
9. Updater muss vor Aktivierung vollständigen Content-Snapshot prüfen, unterbrechungssicher aktivieren und auf letzte gesunde Generation zurückrollen. Bestehende Update-/Repair-Exitcodes beibehalten.
10. Bestehende `.NET`-Konventionen, Lint, Tests und CI beachten. Keine riesigen unreviewbaren Commits. Bei Unsicherheit dokumentieren, keine funktionierende Implementierung vortäuschen.

## Arbeitsphasen und konkrete Ergebnisse

### 0 – Inventarisierung

- Dokumentiere reale Datei-/Providerpfade, Updater-Entrypoints, Release-/Gateway-Datenverträge, externe File APIs, Buildskripte und alle `GetBackingFileName`-Aufrufstellen.
- Lege `docs/asset-packages/architecture.md` an (oder die gelieferte Spezifikation im Repo).
- Entscheide, wo Library, CLI, Testprojekt und Manifestmodelle platziert werden.
- Prüfe, ob die in `README.md` dokumentierte Einschränkung für erforderliche Data-Packages weiterhin gilt; falls ja, dokumentiere die notwendige Entsperrung.

### 1 – NAP-V1-Bibliothek

- Erstelle typisierte Header-/Index-/Chunk-Datentypen, Reader, Writer und Seek-fähigen per-File-Stream mit Zstd-Codec plus `none`.
- Index-/Chunk-Offsets und Größen vor Memory-Allocation/Seek prüfen; maximal zulässige Werte begrenzen.
- Pfadkanonisierung als zentrale Funktion; UTF-8, Slash-Pfade, kein Traversal.
- Writer sortiert Dateipfade und Chunks deterministisch; reproduzierbare Builds bei gleichem Input.
- SHA-256 pro Datei/Chunk; Paket-Hash wird außerhalb des Pakets ins signierte Manifest übernommen.
- Unit-Tests mit synthetischen Samples; Binär-Fixtures und Formatbeschreibung versionieren.
- CLI: `pack`, `inspect`, `verify`.

**Definition of Done:** Pack/Read-Roundtrip binär identisch; Seek + parallele Zugriffe getestet; bewusst beschädigte Archive sicher abgewiesen; Windows/Linux CI grün.

### 2 – VFS-Mounting

- Implementiere `NexusPackageFileProvider : IFileProvider` bzw. einen adapterkompatiblen MountManager.
- Pfade `DATA/`, `data/`, `EXE/../data/` etc. auf denselben virtuellen Namespace abbilden.
- Provider via bestehendes `FileSystem` auf Client und LLServer einbinden; optional Editor. Verzeichnis-Overlay aus Patch 2070 weiterhin ermöglichen.
- Priorität aus Manifest eindeutig mappen (das heutige VFS sucht Provider rückwärts).
- Tombstones und zusammengeführte Verzeichnislisten konsistent realisieren. Bei Bedarf minimale kompatible Erweiterung der Dateisystemabstraktion mit Regressionstests.
- Für reine OS-Dateipfad-Consumer nur verifizierten Cache-Export verwenden.

**Definition of Done:** Ressourcen aus verschiedenen `.nap`-Paketen laden; Overrides/Fallback und Verzeichnislisten stimmen; bestehender loser DATA-Modus weiterhin lauffähig.

### 3 – Manifest und Updater

- Release-Manifest und `active.json` getrennt halten; Ed25519 über dokumentierte kanonische JSON-Form signieren/verifizieren.
- Abhängigkeiten, erforderliche/optionale Pakete, Reihenfolge, Digest, Größen und Plattformanforderungen modellieren.
- Downloader mit Resume, Staging, Download-Locks und Fehlerklassen implementieren; Mirror- und TLS-Policy prüfen.
- Vor atomarer Umschaltung alle Pakete verifizieren. Frühere gesunde Generation erhalten.
- Health-Handshake nach echtem Client-Mount/Start; Rollback bei Startfehler, Crash oder Timeout mit Schleifenschutz.
- Gateway-Version-/Content-Abgleich und Exitcodes 42/43 integrieren; Match zwischen Engine- und Datenprotokoll dokumentieren.

**Definition of Done:** Update, Repair und Rückfall funktionieren unter Netzwerkabbruch, beschädigtem Paket, Crash und zu wenig Speicherplatz; keine gemischten Snapshots.

### 4 – Overlay-Patchbuilder

- Vergleiche zwei effektive Content-Manifeste nach normalisiertem Pfad + Hash.
- Erzeuge `.nap` nur mit neuen/geänderten Dateien und Tombstones für Löschungen.
- `overrides`/Abhängigkeiten deklarieren und bei Konflikt die Ausgabe verweigern.
- Tool für Konsolidierung/Neubasierung der Patchkette und verifizierte Inhaltsgleichheit erstellen.

**Definition of Done:** Alt+Patch liefert exakt denselben virtuellen Dateibaum wie neuer Vollstand; reines Rollback via Manifestwechsel möglich.

### 5 – NAPD (optionales Folgeprojekt)

- `.napd` als Transportrezept, nicht als direkt mountbaren Provider.
- Quelle und Ziel über SHA-256 fixieren; COPY/INSERT/Bounds validieren; auf temporäre NAP-Datei anwenden.
- Delta nur bei positivem Größenvorteil, sonst NAP-Vollpaket; Tests für alle Abbruchpunkte.

### 6 – Audio-Pipeline: WAV durch moderne Formate ersetzen (nach stabiler VFS-Integration)

**Ziel:** Ursprüngliche WAV-Ressourcen optional und reproduzierbar in effizientere Audio-Codecs transkodieren und innerhalb beliebiger `.nap`-Pakete speichern. Alle bisherigen Freelancer-Assetreferenzen müssen ohne INI-Massenänderungen weiter funktionieren. Auslieferung über denselben manifestbasierten Updater inklusive Repair/Rollback.

1. **Codec-Audit vor jeder Festlegung:** Tatsächliche Client-Audio-Ladepfade, Decoder, Codec-Bibliotheken, `IFileProvider`-Verwendung, Audio-Backend, Streaming-Verhalten und Plattformunterstützung im aktuellen Repository untersuchen. Unterstützung für MP3/Vorbis/Opus nicht ungeprüft voraussetzen. Prüfen, ob Decoder ein seekbares Stream-Interface benötigen und ob Audio-Metadaten an den Loader durchgereicht werden.
2. **Formatstrategie:** WAV/PCM als zuverlässigen Fallback erhalten. Für Musik und Ambient-Sounds Ogg Vorbis evaluieren; für Sprache Opus evaluieren; MP3 als optionales Import-/Kompatibilitätsformat. Bei ungeeigneten Decodern, Loop-Anforderungen oder kleinen, latenzkritischen Effekten auf PCM/WAV ausweichen. Keine pauschale Neukodierung bereits verlustbehafteter Quellen.
3. **Separates Build-Modul `Nexus.AudioPipeline`:** Opt-in-CLI-Subcommands `audio analyze`, `audio convert`, `audio verify` (oder gleichwertig), konfigurierbare Codec-Profile je Assetklasse; Toolversionen und Optionen für reproduzierbare Builds protokollieren. Abtastrate/Kanäle nur begründet konvertieren, Spitzenpegel und Clipping kontrollieren.
4. **Alias-/Resolver-Schicht:** Manifest-Mapping von kanonischem altem Pfad (z. B. `DATA/AUDIO/MUSIC/theme.wav`) auf tatsächlich im Paket vorhandenen Pfad/Codec (z. B. `AUDIO/MUSIC/theme.ogg`). Auflösung nur im Audio-Ladepfad, *nicht* pauschal jede `IFileProvider.Open("*.wav")`-Anfrage in fremde Bytes verwandeln. Aufrufer, die WAV erwarten, müssen weiterhin gültige WAV-Bytes bekommen oder einen explizit erweiterten Decoderpfad nutzen.
5. **NAP-Speicherstrategie:** Vorbis/Opus/MP3/FLAC bereits komprimiert speichern (`compression=none` standardmäßig); WAV/PCM optional Zstd; Chunking und Read/Seek auf Streaming/Decoderbedarf abstimmen. Für lange Audiodateien kein obligatorisches Voll-Decoding in den Speicher.
6. **Looping und Latenz:** Original-Loops (Loop-Start/Ende, Repeat, Gapless-/Pre-Skip/Encoder-Delay), 3D-/Positional-Audio, kurzer SFX-Start, Stopp/Restart, Kanalzuordnung und Sample-Timing prüfen. Fehlende Loop-Metadaten nicht erfinden; bei nicht exakt reproduzierbarem Verhalten WAV behalten.
7. **Updater & Versionierung:** Audio-Assets in eigenständigen Paketen (z. B. `sounds.nap`, `music.nap`, `voices-de.nap`), unabhängig patchbar. Im Manifest Codec- und Decoder-Mindestversion, Ressourcenalias, Inhalts-Hash und Paketabhängigkeiten angeben. Vor Aktivierung Kompatibilität prüfen; gemischte Audio-/Content-Snapshots vermeiden.
8. **Sicherheit und Recht:** Decoding und Metadaten aus nicht vertrauenswürdigen Paketen mit Bounds/Duration-Limits absichern. Keine urheberrechtlich geschützten Originalassets in Repo, CI oder öffentliche Releases hochladen. Konvertieren ändert nicht die Verbreitungsrechte.

**Tests / Definition of Done:** Synthetische kurze SFX, lange Musik, Sprache, Stereo/Mono, absichtliche Decoderfehler, Seek- und parallele Playback-Zugriffe; Audio-Alias und Legacy-WAV ohne Regression; mehrfache Wiedergabe und Loops ohne hörbare Aussetzer oder unzulässige Startverzögerung; Windows-/Linux-Builds; Audio-Paket-Update, Repair und Rollback erfolgreich. Audio-Qualität anhand AB-/Hörtests und objektiven Metriken bewerten. Wenn exakte Loop-Gleichheit nicht sichergestellt ist, dokumentierter PCM-Fallback.

**Lieferumfang:** `docs/asset-packages/audio-pipeline.md`, Codec-Kompatibilitätsmatrix mit Codebelegen, CLI/Builder-Tests, Laufzeittests und per-Asset konfigurierbare Konvertierungsprofile. Die Arbeit an Phase 6 erst nach einer abgeschlossenen und getesteten VFS-/Updater-Basis beginnen.

## Testmatrix

| Szenario | Erwartung |
|---|---|
| INI/CMP/DDS/ALE/WAV und Zufallsbytes | Byteidentischer Roundtrip |
| Datei zweimal in unterschiedlichen Containern | Manifest-Priorität gewinnt |
| Tombstone überschreibt ältere Datei | Datei wird als nicht vorhanden angezeigt |
| Gemischte Groß-/Kleinschreibung | Gleicher logischer Pfad unter Windows/Linux |
| `..`/absoluter Pfad im Paket | Rejection ohne Nebenwirkungen |
| beschädigter Chunk / Index | Keine unsichere Rückgabe, klarer Fehler |
| Seek und paralleler Read | Korrekte Bytes, begrenzter Cache |
| Download-Abbruch / Resume | Verifizierbare Fortsetzung oder sicherer Neustart |
| Crash vor/bei/nach Manifest-Switch | Vorheriger oder neuer vollständiger Zustand |
| Start ohne erfolgreiches Health-Signal | Rollback auf letzten gesunden Snapshot |
| Standalone-Verzeichnisinstallation | Keine Regression |
| optionales Sprach-/Texturpaket | Installier-/deinstallierbar ohne Gameplay-Konflikt |
| WAV-Alias auf Vorbis/Opus | Audio-Ladepfad spielt korrekt; Legacy-WAV-Consumer bleiben kompatibel |
| Musik-Loop und kurzer SFX | Korrekte Loop-Grenzen bzw. PCM-Fallback, kein unnötiger Start-Delay |
| Komprimiertes Audio in NAP | Ohne zusätzliches Zstd und ohne Voll-Decoding langer Streams |

## Erwartete Ausgaben je Umsetzungsschritt

- Änderungen im passenden Modul und gepflegten Patchoverlay.
- Reproduzierbare Befehle für Build und Tests.
- Ein Bericht `docs/asset-packages/implementation-status.md` mit **implemented / partial / planned**, tatsächlichen Pfaden, Risiken und offenen Punkten.
- Tests und Build-Resultate; bei Fehlschlägen präzise Ursachen.
- Kurze Migrationsanleitung `DATA/` -> lokale `.nap`-Dateien (ohne Weitergabe fremder Assets).
- Keine Behauptung von erfolgreicher Integration ohne ausführbaren End-to-End-Test.

## Agenten-Startauftrag (direkt verwendbar)

> Lies zuerst `AGENTS.md`, `README.md`, die aktuelle Patchserie und `NEXUS-ASSET-PACKAGES.md`. Inventarisiere VFS und Updater vollständig. Erstelle dann einen Auditbericht mit konkreten Quellcodepfaden. Berücksichtige Phase 6 (Audio-Pipeline, WAV-Kompatibilität, Vorbis/Opus/MP3, Streaming und Looping) in der Gesamtarchitektur, implementiere sie aber noch nicht. Implementiere **nur Phase 1** des NAP-V1-Formats einschließlich Reader/Writer/CLI/Tests; stoppe danach an einer überprüfbaren Übergabestelle und dokumentiere die nächsten Integrationsschritte. Respektiere die Patch-Architektur und den Standalone-Modus. Verwende ausschließlich synthetische Test-Assets. Führe vorhandene Tests aus und berichte tatsächliche Ergebnisse.
