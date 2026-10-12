# Freelancer Data: offene Client-Arbeiten

Diese Liste überträgt aus dem benachbarten [Data-Repository](../../Data/README.md)
die Aufgaben, die LibreLancer-Client, Engine, Renderer oder Laufzeit betreffen.
Die Quelldokumente bleiben maßgeblich für Änderungen an SDK-INIs, NAP-Dateien
und dem Datenimport. Originaldateien werden nicht in diesem Client-Repository
kopiert oder committet.

Stand der Sichtung: 2026-10-11. Die Quell-TODOs enthalten teils veraltete
Statuszeilen wie „Änderungen sind noch nicht committet“; diese wurden nicht als
Client-Aufgaben übernommen.

Die Reihenfolge, Abhängigkeiten und Abnahmekriterien zur Umsetzung stehen im
[Client-Umsetzungsplan](data-client-plan.md).

## Client-Aufgaben

### Musik-Playlists in Räumen

Quelle: [Data/TODO-musik.md](../../Data/TODO-musik.md)

- [x] Den Ablauf vom `[Room_Sound] music`-Parser über `GameItemDb` und
  `BaseRoom` bis `RoomGameplay`/`SoundManager.PlayMusic` nachvollziehen.
- [x] Kommagetrennte Nicknames in einer `music`-Zeile unterstützen; einzelne
  Nicknames und das bestehende `oneshot`-Suffix bleiben kompatibel.
- [x] Playlist-Regeln festlegen: zufälliger Track beim Raumwechsel, danach
  Weiterschalten nach Trackende ohne direkte Wiederholung. Ein Einzeltitel
  bleibt geloopt. Eine Playlist mit `oneshot` spielt genau einen zufällig
  gewählten Track und endet; es gibt keinen Timerwechsel.
- [x] Parser- und Auswahltests für mehrere Titel, `oneshot`, Einzeltrack,
  Leereintrag und Vermeidung direkter Wiederholungen ergänzen.
- [ ] Wiedergabe und Trackwechsel in `Br0104Bar` mit dem lokalen
  Freelancer-Datenbestand zur Laufzeit prüfen.
- [ ] Erst nach erfolgreichem Engine-Test die Bar-Raum-INIs im Data-Repository
  auf Playlists umstellen und anschließend dessen INIs nach NAP synchronisieren.

### Asteroidenfelder und Asteroidentexturen

Quelle: [Data/TODO-asteroiden.md](../../Data/TODO-asteroiden.md)

- [ ] Die aus der HD-Edition übernommenen Abstände (`fill_dist`, `start_dist`),
  Dichte und Farben in den betroffenen Systemen im Client darstellen und mit
  dem Originalverhalten vergleichen.
- [ ] Für die neu verwendeten Abschnitte und Felder (`shape`, `exclusion`,
  `zone`, `render_parts`, `fade`) prüfen, ob Parser, GameItemDb und Renderer
  Werte nicht nur einlesen, sondern korrekt verwenden. Fehlende oder
  unbekannte Werte mit gezielten Parsertests abdecken.
- [ ] Die Asteroidenformen und Feldgrenzen in repräsentativen Systemen prüfen;
  Fehler bei Laden, Sichtbarkeit, Dichte oder Absturz reproduzierbar festhalten.
- [ ] Fehlende Referenzen auf `solar/asteroids/roids.txm` im Client-Lauf
  identifizieren und dokumentieren. Textur ersetzen oder SDK-Verweis ändern
  bleibt eine Entscheidung und Änderung im Data-Repository.

### Schwarze Löcher, Nebel und Ringsysteme

Quelle: [Data/TODO-blackhole.md](../../Data/TODO-blackhole.md)

- [ ] Das Omega-13-Schwarze-Loch sowie betroffene Ringsysteme im Client laden und
  Darstellung, Sichtbarkeit, Speicherbedarf und Laufzeitstabilität prüfen.
- [ ] Gegenüberstellen, wie sich das Entfernen von `[Sound]`, `[FadeChunks]`
  und `[CloseChunks]` auswirkt. Die dauerhafte Datenentscheidung bleibt im
  Data-Repository; hier werden die tatsächlich unterstützten Engine-Pfade und
  Laufzeitfolgen festgehalten.
- [ ] Die `rock_wisp`-Puffformen und gemeinsam genutzten HD-Ringformen/-texturen
  gegen die verfügbaren Assets prüfen; fehlende Ressourcen und Parser-/Renderer-
  Grenzen mit konkreten Logs oder Screenshots dokumentieren.
- [ ] Tests für relevante Parserfelder und fehlende optionale Ressourcen
  ergänzen, sobald der Laufzeittest eine konkrete Client-Lücke zeigt.

### Solar-, Planeten- und Nebelwerte

Quelle: [Data/TODO-solar.md](../../Data/TODO-solar.md)

- [ ] Planetendarstellung mit den flachen Materialpfaden (`solar/planets/*.mat`
  und `detailmap_*.txm`) im Client prüfen; Material- und Texturauflösung im
  Laufzeitlog festhalten.
- [ ] Die 59 geänderten Nebelkonfigurationen sowie Sterne, Sonnen und
  Asteroiden-/Sonnen-Loadouts in repräsentativen Systemen visuell und auf
  Laufzeitfehler prüfen.
- [ ] Von der HD-Edition stammende Solar-INI-Felder auf Parser- und
  Renderer-Verwendung prüfen. Ein erfolgreiches Parsen allein gilt nicht als
  Laufzeitnachweis.
- [ ] Die fehlenden `solar/asteroids/roids.txm`-Verweise zusammen mit dem
  Asteroiden-TODO erfassen, damit Daten- und Clientprüfung dieselbe Ausnahme
  verwenden.

### Startup-Videos und moderne Videoformate

Quelle: [Data/TODO-video.md](../../Data/TODO-video.md)

- [x] `StartupMovies`-Verwendung geprüft: `FreelancerIni` parst
  `movie_file`; im Client-Quellbaum wurde kein Verbraucher und kein
  Wiedergabeaufruf gefunden. Der Playback-Pfad ist derzeit nicht angebunden.
- [ ] Entscheiden und planen, ob Startup-Movie-Wiedergabe im Client umgesetzt
  werden soll; bei Umsetzung Playback-Pfad und Decoder für Zielplattformen
  ergänzen und testen.
- [ ] Unterstützte Video-/Audioformate und Decoder auf den Zielplattformen
  feststellen. MP4/H.264/AAC erst dann als Clientformat freigeben, wenn ein
  echter Wiedergabetest im Client gelingt.
- [ ] Für einen synthetischen oder anderweitig rechtmäßig bereitgestellten
  Testclip Bild, Ton, Synchronität, Abbruch und Fehlerverhalten prüfen; keine
  Originalvideos als Testfixture in Git ablegen.
- [ ] WMV als Spielasset beibehalten, bis Client- und Zielsystemtests ein
  alternatives Format bestätigen. Upscaling, Konvertierung, NAP-Ablage und
  `build_nap.py`-Schutz vor Überschreiben bleiben Data-Aufgaben.

### Mehrsprachige Texte und Sounds in NAP

Die Datenimport- und NAP-Aufgaben stehen im
[Data-Umsetzungsplan](../../Data/TODO-mehrsprachigkeit.md). Client-Aufgaben:
Die vorhandenen Decoderpfade sind im [Audio-Pipeline-Audit](asset-packages/audio-pipeline.md)
beschrieben.

- [ ] Versioniertes JSON-Ressourcenmanifest aus NAP laden und anstelle der drei
  Laufzeit-DLLs `InfoCards.dll`, `MiscText.dll` und `NameResources.dll`
  verwenden. Numerische IDs und DLL-Reihenfolge für zusammengesetzte IDs
  erhalten; RDL-Infocarddaten unverändert an den Renderer geben.
- [ ] Spracheinstellung im Client mit Text-/Infocardauflösung und NAP-VFS
  verbinden. BCP-47-Sprach-/Regionsauswahl und Fallback
  `Sprache-Region → Sprache → Standard` umsetzen; Sprachwechsel muss Texte
  ohne Änderung der Spiel-IDs neu laden können.
- [ ] Sprachabhängige Sprach-/Dialogaufnahmen und weitere lokalisierte Sounds
  über dieselben Sprachkennungen und Fallback-Regeln aus NAP auswählen.
  Sprachneutrale SFX und Musik bleiben gemeinsam nutzbar; Audioformate und
  Decoder laufen weiterhin über die bestehende Audio-Pipeline.
- [ ] Tests für DLL-zu-JSON-Parität (IDs und Inhalte), ungültige JSON-Daten,
  Sprach-/Regions-Fallback und NAP-Laden ergänzen. Zur Laufzeit Hauptmenütexte
  in mindestens zwei Sprachen sowie je einen lokalisierten Dialog-/Soundpfad
  prüfen; fehlende Original-Ressourcen-DLLs dürfen dafür im Clientpaket nicht
  vorausgesetzt werden.

## Gemeinsame Prüfpunkte und Zuständigkeit

- [ ] Eine Client-Ladeprüfung mit einer lokalen Freelancer-Installation und
  einem aufgebauten NAP-Datenbestand durchführen; keine Asset-Dateien aus der
  Installation in dieses Repository kopieren.
- [ ] Fehlende Asset-Verweise vor Client-Fehlerzuordnung mit dem Data-Inventar
  abgleichen. `Data/README.md`, `Data/WISSEN.md` und `Data/missing.md` nennen
  derzeit unterschiedliche Zahlen für fehlende Ziele; diese Bestandszählung
  muss im Data-Repository vereinheitlicht werden.
- [ ] Für jeden reproduzierten Client-Fehler einen synthetischen Parser-/Engine-
  Regressionstest ergänzen und bei Engineänderungen die Client-Patchserie
  aktualisieren.

## Im Data-Repository belassen

Diese Punkte aus den Quelldokumenten werden hier nicht als Client-Codearbeit
dupliziert:

- HD-INI-Werte, Verweise und Entscheidungen zu fehlenden Dateien ändern.
- Musik-Playlists in Raum-INIs pflegen und SDK-INIs nach NAP synchronisieren.
- Videos hochskalieren/umkodieren, NAP-Inhalte bereitstellen und
  `tools/build_nap.py` bzw. `tools/hd_overrides.txt` ändern.
- `classes.md`, `missing.md`, `WISSEN.md` und den Data-Changelog pflegen.

## Daten-Quellen

- [Asteroiden](../../Data/TODO-asteroiden.md)
- [Schwarze Löcher](../../Data/TODO-blackhole.md)
- [Musik](../../Data/TODO-musik.md)
- [Solar und Nebel](../../Data/TODO-solar.md)
- [Video](../../Data/TODO-video.md)
