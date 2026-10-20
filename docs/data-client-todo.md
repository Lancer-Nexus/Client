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
- [x] Wiedergabe und Trackwechsel in `Br0104Bar` mit dem lokalen
  Freelancer-Datenbestand zur Laufzeit geprüft. Der isolierte Debug-Lauf nutzte
  eine Retail-DATA-Kopie, startete `Br01_04_Base` in der Bar und ersetzte die
  Raum-Musik durch zwei synthetische Ogg-Tracks. Das Log zeigt zehn Trackwechsel
  im Wechsel `test_bar_b` / `test_bar_a`, ohne Error-, Fatal- oder
  Exception-Einträge; OpenAL lief im Null-Treiber, daher kein Hörtest.
- [x] Danach 131 generische Bar-Raum-INIs im Data-Repository auf die zehn
  generischen Titel umgestellt und alle 1252 SDK-INIs nach NAP synchronisiert.
  Nach Erhalt der CRLF-Zeilenenden wurden 131 Ziele aktualisiert, 1121 blieben
  unverändert; der finale Dry-Run meldete 1252 unveränderte Dateien.
  Alle 131 Bar-Ziele in `NAP/World` stimmen byteweise mit den SDK-INIs überein.

### Asteroidenfelder und Asteroidentexturen

Quelle: [Data/TODO-asteroiden.md](../../Data/TODO-asteroiden.md)

- [ ] Die aus der HD-Edition übernommenen Abstände (`fill_dist`, `start_dist`),
  Dichte und Farben in den betroffenen Systemen im Client darstellen und mit
  dem Originalverhalten vergleichen.
- [x] Codepfad für Feldwerte, Billboards, Band und Exclusion-Zonen von Schema
  über `GameItemDb` bis Renderer/Komponente geprüft. `fill_dist`,
  `empty_cube_frequency`, Farben, Billboard-`start_dist` und
  `fade_dist_percent` werden verwendet; `Band.render_parts`/`fade` werden
  gespeichert, aber nicht gerendert. `ExclusionBand`, mehrere Band-/Billboard-
  Felder sowie die Exclusion-Steuerflags sind derzeit nicht oder nur teilweise
  angebunden. Details stehen im [Client-Umsetzungsplan](data-client-plan.md#phase-1--parser--und-datenflussprüfung).
- [x] Retail-Laufzeitbaseline für BR01 Cornwall aufgenommen: ein isolierter
  Debug-Client mit temporär missionsfreiem Startsave betrat das Zentrum des
  Asteroidenfelds und zeigte Asteroiden in der Szene. Log, Warnungen und
  Screenshot liegen unter `/tmp/lancer-br01-asteroid-runtime-20261012/`.
  Dieser Lauf prüft weder HD-Werte noch das Originalspiel.
- [x] Client-A/B-Lauf für Cornwall mit Retail-Feld-INI und HD-SDK-Feld-INI
  durchgeführt. Beide Varianten erreichten `Entering system Br01`; der HD-Lauf
  lud das Feld nach Bereitstellung der referenzierten SDK-`RockShapes.ini`
  ebenfalls ohne Error/Fatal/Exception. Die INI-Werte ändern gegenüber Retail
  `fill_dist` von 1650 auf 3300 und `start_dist` von 1000 auf 2000; die
  Würfelhäufigkeit (0,5), Feldfarben und Asteroidenwürfel bleiben gleich.
  Quellcode und Lauf bestätigen den erweiterten Render-Radius und die spätere
  Billboard-Einblendung im Client. Logs und Vergleichsbilder liegen unter
  `/tmp/lancer-proton-freelancer-asteroid-20261012/`. Das ist kein Vergleich
  mit dem Originalrenderer.
- [ ] Originalspielvergleich bleibt offen. Proton 7.0 und portables Wine 10.20
  endeten vor der Szene mit „Freelancer did not detect a 3D capable video
  card“; DXVK scheiterte an `VK_KHR_surface`. Ein Proton-9-Beta-Lauf auf dem
  Intel-HD4600-Desktop rendert mit einer vollständigen, temporären Retail-Kopie
  nun das animierte Freelancer-Titelmenü und dessen 3D-Hintergrund. Die Kopie
  enthält `DATA`, `EXE` und `DLLS` unter
  `/tmp/lancer-proton-freelancer-asteroid-20261012/FL/`; die Retail-Installation
  blieb unverändert. `xdotool`/XTest und isolierte uinput-Tastatur-/Mausereignisse
  wählten „New Game“ nicht aus. Deshalb ist weiterhin keine Retail-Szenenreferenz
  für den A/B-Lauf vorhanden. Log und Menüaufnahme liegen im selben
  `/tmp/lancer-proton-freelancer-asteroid-20261012/`-Verzeichnis.
  Im Wiederholungslauf erschien der Grafikkartenhinweis erneut; Win32
  `BM_CLICK` auf `IDYES` schloss ihn. Ein anschließender Win32-SendInput- und
  PostMessage-Klick auf „New Game“ löste keinen Szenenwechsel aus. Die X11-
  Aufnahme war danach schwarz, während das Spiel weiter Renderwarnungen schrieb;
  der sichtbare Szenenzustand dieses Wiederholungslaufs ist daher unklar.
  Auch mit `fullscreen = 0` und `size = 1024,768` in der temporären
  `freelancer.ini` blieb die Fensteraufnahme nach dem Warnhinweis schwarz. Die
  Konfiguration wurde danach auf den Retail-Stand zurückgesetzt.
- [x] HD-Asteroidenreferenzen statisch abgeglichen: alle 210
  `[Asteroids] file`-Verweise aus SDK-System-INIs zeigen auf Dateien in
  `SDK/solar/asteroids/`; der Sync-Trockentest legt BR01s System- und
  Asteroiden-INI unter `NAP/World/` mit passendem Pfad ab. Kein statischer
  Dateinamenbruch gefunden. Der echte NAP-Bestand ist weiterhin leer, daher
  bleibt der Paket-/HD-Clientlauf offen.
- [ ] Ungeklärte HD-Semantik und sichtbare Wirkung der erfassten Lücken in
  `Br01CornwallRockAsteroidField.ini` mit dem Originalspiel vergleichen;
  anschließend nur bestätigte Client-Abweichungen gezielt korrigieren.
- [x] Retail-DATA und SDK für die offenen `AsteroidArch.ini`-Felder
  abgeglichen: dieselben drei `[AsteroidMine]`-Einträge besitzen
  `explosion_impulse` (1000, 100, 1000) und dieselben drei
  `[DynamicAsteroid]`-Einträge besitzen `particle_effect` (`gf_blsmallasteroid`
  einmal, `gf_dynamiclavaast` zweimal).
- [x] Clientpfad implementiert: `DynamicAsteroid` parst `particle_effect`,
  `GameItemDb.InitAsteroids()` löst ihn über `Effects.ini` zu `ResolvedFx` auf,
  und `CGameSession` hängt den `ParticleEffectRenderer` mit der Effektlichtkurve
  an das dynamische Asteroidenobjekt. Drei Parser-Regressionen und der Release-
  Build bestehen; ein frischer NAP-Start erreicht `FP7_system` ohne die früheren
  `particle_effect`-Parsewarnungen. Ein sichtbarer Asteroidenfeld-Laufzeittest
  bleibt offen. `explosion_impulse` wird weiterhin nicht übernommen.
- [x] Retail-Binärpfad für `particle_effect` identifiziert: Ghidra findet in
  `Freelancer.exe` den Parser `FUN_00408cd0` bei `0x00408cd0`; er liest den
  Effektwert, bildet eine ID und speichert sie im Objekt. Das belegt das Parsen,
  aber noch nicht, wann oder ob dieser Effekt abgespielt wird. Für
  `explosion_impulse` fand die Literalsuche in den installierten EXE/DLLs keinen
  direkten Treffer; das allein beweist nicht, dass der Schlüssel ignoriert wird.
- [ ] Minenexplosion und Zerstörung dynamischer Asteroiden im Originalspiel
  gezielt beobachten und mit dem Client vergleichen. Der `particle_effect`-
  Eintrag wird jetzt bis zum Renderer ausgeführt; Retail-Parität und sichtbarer
  Effekt im Asteroidenfeld bleiben offen. Die Bedeutung von `explosion_impulse`
  ist ungeklärt und wird bis zu einem belegten Verbraucher nicht umgesetzt.
- [ ] Die Asteroidenformen und Feldgrenzen in repräsentativen Systemen prüfen;
  Fehler bei Laden, Sichtbarkeit, Dichte oder Absturz reproduzierbar festhalten.
- [x] Die sieben `solar/asteroids/roids.txm`-Verweise der SDK-`Shapes.ini` bis
  zum Clientpfad geprüft. Weder Retail-DATA noch SDK-System-/Asteroiden-INIs
  referenzieren `solar/asteroids/Shapes.ini`; auch ihre 15 Shape-Namen werden
  außerhalb der Datei nicht verwendet. Der aktuelle Datenbestand aktiviert
  diesen Fehler daher nicht. Würde eine Konfiguration diese Shapes verwenden,
  kann der fehlende Textureintrag im Billboard-Renderer zu einer
  Nullreferenz führen. Die Retail-Laufzeitbaseline BR01 benutzt
  `RockShapes.ini` und prüft diesen Sonderfall nicht.
- [ ] Falls Data eine aktive Konfiguration für `Shapes.ini` ergänzt, den
  betroffenen Client-Lauf reproduzieren. Textur ersetzen oder SDK-Verweis
  ändern bleibt eine Entscheidung und Änderung im Data-Repository.

### Schwarze Löcher, Nebel und Ringsysteme

Quelle: [Data/TODO-blackhole.md](../../Data/TODO-blackhole.md)

- [x] Lokale Retail- und SDK-`Bh.ini` read-only verglichen: SDK entfernt
  `[Sound]`, `[FadeChunks]`, `[CloseChunks]` und ersetzt `lava_wisp` durch
  `rock_wisp`.
- [x] SDK-Systemreferenzen sowie Client-Schemas und Renderer nach einem
  Black-Hole-Pfad geprüft. Kein Systemverweis und kein dedizierter Client-Parser/
  Renderer gefunden; unterstützte Nebel-Schemas decken mehrere Black-Hole-Felder
  nicht ab. Details: [Client-Umsetzungsplan](data-client-plan.md#phase-1--parser--und-datenflussprüfung).
- [x] Nebelpfad von Schema über `GameItemDb.GetNebula()` bis
  `NebulaRenderer`/`SunRenderer` geprüft. Nicht angebundene SDK-Werte und
  Mehrfach-`NebulaLight`-Abschnitte sind im
  [Client-Umsetzungsplan](data-client-plan.md#phase-1--parser--und-datenflussprüfung)
  festgehalten.
- [x] Planetare `ring`-Dateireferenzen bis ins Datenmodell verfolgt; ein
  Renderer-Verbraucher fehlt im aktuellen Quellbaum. `Zone.ShapeKind.Ring`
  funktioniert separat als Geometrie für Zonenabfragen.
- [ ] Nebel- und Ringbefunde im Originalspiel visuell vergleichen; insbesondere
  die Omega-13-`rock_wisp`-Puffs und die planetaren Ringassets prüfen.

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

- [x] Solar-Archetypen bis `ResolveDrawable()` und zum Modellladen des
  Systemobjekts verfolgt. `material_library` und `DA_archetype` verwenden die
  exakt angegebenen VFS-Pfade; fehlende Dateien werden als Fehler protokolliert
  und es gibt keinen Flatten-/Alias-Fallback. Das HD-INI passt seine flachen
  `.mat`-/`.txm`-Pfade an die Paketnamen an. `Data/NAP/Material` enthält lokal
  nur `.gitkeep`. Der read-only Retail-DATA-Lauf reproduziert 932
  `SolarArch.ini`-Verweise, weist aber `detailmap_gas.txm` (9 Stellen) und
  `detailmap_ice.txm` (12 Stellen) als fehlend aus. Das kann der vollständige
  NAP-Bestand mit HD-Dateien ergänzen; der Paket-/Renderlauf bleibt offen.
  Details:
  [Client-Umsetzungsplan](data-client-plan.md#phase-1--parser--und-datenflussprüfung).
- [x] `solar_radius` bis Navmap und Besuchsradius, `atmosphere_range` bis
  `GameWorld.InAtmosphere()` verfolgt. `burn_color`/`ambient_color` werden
  kopiert, aber im Spielquellbaum nicht verwendet.
- [x] `StarArch.ini` geprüft: Glow, Center und Spines erreichen `SunRenderer`;
  Lens-Glow-Form, Farbe, Radius und Fade-Zeiten sind nun ebenfalls angebunden.
  `lens_flare` sowie die Stern-Intensitäts- und Zonen-Fades bleiben Parser-only;
  der visuelle Originalabgleich ist offen. Details: [Client-Umsetzungsplan](data-client-plan.md#phase-1--parser--und-datenflussprüfung).
- [x] Schlüssel-zu-Code-Abgleich vervollständigt: 14 von 28 `SolarArch.ini`-
  Schlüsseln werden in das Solar-Laufzeitmodell übernommen; bei `StarArch.ini`
  erreichen 6 von 11 Schlüsseln den Renderer. Die geparsten, aber im Solar-/Stern-
  pfad nicht angebundenen Schlüssel und getrennte Schiffverbraucher sind im
  [Client-Umsetzungsplan](data-client-plan.md#phase-1--parser--und-datenflussprüfung)
  dokumentiert.
- [ ] HD-Planetmaterialien und geänderte Stern-/Solarwerte im repräsentativen
  Client-Lauf visuell prüfen.
- [x] Stock-Renderbaseline im separaten Debug-Client-Lauf festgehalten:
  `FP7_system` aus lokaler Retail-DATA geladen; Screenshot zeigt Schiffe,
  Station, Sterne und Nebel. Log bestätigt Daten- und UI-Laden sowie den
  Systemeintritt, mit 108 Warnungen und ohne Error/Fatal/Exception. Beleg:
  `/tmp/lancer-solar-runtime-20261012/`. Der Lauf verwendet weder HD-/NAP-
  Inhalte noch Clusterwelten und vergleicht nicht mit dem Originalspiel.
- [ ] Planetendarstellung mit den flachen Materialpfaden (`solar/planets/*.mat`
  und `detailmap_*.txm`) im Client prüfen; Material- und Texturauflösung im
  Laufzeitlog festhalten.
- [ ] Die 59 geänderten Nebelkonfigurationen sowie Sterne, Sonnen und
  Asteroiden-/Sonnen-Loadouts in repräsentativen Systemen visuell und auf
  Laufzeitfehler prüfen.
- [x] Parserdeklarationen der 28 unterschiedlichen `[Solar]`-Schlüssel aus
  `SDK/solar/SolarArch.ini` mit `Schema/Archetype.cs` abgeglichen; alle besitzen
  ein `[Entry]` oder `[EntryHandler]`, kein unbekannter Schlüssel gefunden.
- [ ] HD-Planetenmaterialien und geänderte Stern-/Solarwerte im repräsentativen
  Clientlauf visuell prüfen. Der Quellcode-Abgleich belegt keine korrekte
  Darstellung; der vollständige HD-NAP mit den Texturassets fehlt lokal noch.
- [x] Fehlende `solar/asteroids/roids.txm`-Verweise zusammen mit dem
  Asteroiden-TODO erfasst; die sieben Einträge sind derzeit nicht über die
  Retail- oder SDK-Konfiguration erreichbar.

### Startup-Videos und moderne Videoformate

Quelle: [Data/TODO-video.md](../../Data/TODO-video.md)

- [x] `StartupMovies`-Verwendung geprüft: `FreelancerIni` parst
  `movie_file`; im Client-Quellbaum wurde kein Verbraucher und kein
  Wiedergabeaufruf gefunden. Der Playback-Pfad ist derzeit nicht angebunden.
- [x] Decoder-Inventar geprüft: in `src` ist kein Video-Decoder oder
  Video-Wiedergabe-Backend eingebunden. Die HD-Data-Dokumentation führt
  WMV9/VC-1 mit WMA als vorhandene Movie-Formate auf.
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

### Ogg-/FLAC-Musik und Sounds

Quelle: [Audio-Pipeline-Audit](asset-packages/audio-pipeline.md)

- [x] Direkte AudioEntry-Wiedergabe geprüft: der native `lancerdecode`-Pfad
  erkennt Ogg/Vorbis und FLAC per Signatur und dekodiert beide zu PCM.
- [x] Musik-Loop geprüft: der alte Vorbis-Reset nach EOF startete den Stream
  nicht erneut. Patch 2172 öffnet den Decoder ab Streamanfang neu; Vorbis-
  und FLAC-Resetregressionen bestehen 2/2 mit dem 8192-Byte-Musikpuffer.
- [x] SFX-Loop-Pfad abgegrenzt: Einzelsounds werden zuerst vollständig zu PCM
  dekodiert; wiederholte Wiedergabe nutzt OpenAL `AL_LOOPING` und hängt nicht
  von einem Vorbis-Decoderreset ab.
- [x] NAP-gebundene Vorbis-, FLAC- und Ogg-FLAC-SFX aus dem aktiven Snapshot
  über `SoundData`/`AudioManager` abgespielt: Loop-Instanzen blieben nach 2,5 s
  aktiv, One-Shot-Kontrollen endeten nach 1,4 s. Die Client-SFX-Trigger in
  einer Spielsituation sind damit nicht gesondert abgenommen.
- [x] Raum-Playlistintegration mit synchronisiertem Debug-Paket nachgewiesen:
  `MusicPlayer` meldet Streamende, `RoomGameplay` wählt daraufhin A/B
  abwechselnd; 20-Sekunden-Clientlauf und Dateiöffnungen belegen den Übergang.
- [x] Nichtstille Client-PCM-Ausgabe im 44,1-kHz-Stereo-PulseAudio-Monitor
  nachgewiesen (-24 dBFS Spitzenpegel), geroutet auf einen temporären Null-Sink.
- [ ] Vorbis-Musik und Playlistwechsel im echten Client auf Lautsprecher oder
  Kopfhörer anhören und Loop-Übergangsqualität beurteilen.
- [x] Audio-Decoder mit Vorbis, FLAC und Ogg FLAC aus chunked NAP-Einträgen
  lesen, zurücksetzen und byte-identische PCM-Ausgaben prüfen.
- [x] Aktiven NAP-Paketbestand im vollständigen Linux-Client ohne loses
  `DATA` starten. Der Debuglauf lud Spieldaten aus dem aktiven Snapshot und
  wählte zwei NAP-gebundene Vorbis-Tracks abwechselnd; beide Testtöne sind in
  der Null-Sink-PCM-Aufnahme im Frequenzspektrum erkennbar. Ein separater
  Ein-Track-Lauf mit `oneshot=False` hielt den 440-Hz-Vorbis-Track über EOF
  hinaus aktiv; die akustische Loop-Grenze bleibt zu beurteilen.
- [x] `lancerdecode` als x86-64 Windows-DLL cross-kompiliert; PE32+ und
  `KERNEL32.dll`/`msvcrt.dll`-Importe geprüft.
- [x] Der BuildLL-Lauf `BuildEngine --with-win64` legt die x64-PE32+-DLL
  bytegleich unter `bin/librelancer-win-x64/lib/x64/` ab.
- [ ] Windows-Ladeauflösung und Fixture-Decodierung im CI-Runner bestätigen; der Workflow-Schritt ist eingerichtet, ein erfolgreicher Lauf steht noch aus.
- [x] Ogg FLAC separat mit synthetischem Fixture getestet: Container-/Codec-
  Erkennung, PCM-Decoding, Reset nach EOF und Musik-/SFX-Loopkontrollen
  bestehen unter Linux.
- [x] Synthetische FLAC- und Ogg-FLAC-Musik aus dem aktiven Linux-NAP-Client
  gestreamt und im Null-Sink-Spektrum bestätigt; beide Formate wechselten in
  der Deck-Playlist wiederholt.
- [x] Vollständige `LibreLancer.Tests`-Suite in Release am 2026-10-12 bestanden:
  677/677 Tests. Das ergänzt den fokussierten Audio-/Playlistlauf (12/12),
  bestätigt für sich allein aber keine Windows-Laufzeit oder Hörqualität.
- [ ] Vorbis/FLAC/Ogg FLAC unter Windows mit gestagter DLL auf Seek-/Chunk-
  grenzen prüfen; Loaderauflösung und Laufzeit-Decodierung sind offen.

### Grafische Erweiterungen: Normalmaps, Cubemaps und Vulkan

- [ ] Den [Grafik-Umsetzungsplan](graphics-implementation-plan.md) in der
  Reihenfolge P0–P9 abarbeiten. Zuerst den NAP-Bestand vervollständigen und
  die vorhandenen Surface-Karten mit INI-Auflösung, Tangenten und Material-
  Parametern im Client nutzbar machen; anschließend IBL, Render-Passes,
  Post-Processing und das vollständige SDL_GPU/Vulkan-Backend abnehmen.
- [ ] Die [Data-Aufgaben](../../Data/TODO-surface-maps.md) und das
  [aktuelle Inventar](evidence/graphics-input-inventory-20261012.json)
  für die leeren INIs/Bibliotheken und den unvollständigen Container verwenden.

### Figuren-Gesichter und Charaktertexturen

- [ ] Gesichter und sichtbare Hautmaterialien ausgewählter Figuren weniger
  plastikartig gestalten. Zuerst die passenden NAP-Texturen, UVs, MAT-Zuweisungen
  und vorhandenen Normalmaps/Rauheitsdaten einem konkreten Charaktermodell
  zuordnen. Danach optionale, per Image-to-Image-LLM überarbeitete Albedo-Maps
  sowie dazu passende Normal-/Roughness-Maps erzeugen und im isolierten NAP-Test
  mit den Originalen vergleichen. UV-Nähte, Beleuchtung, Gesichtsidentität und
  Dialog-/Raumansichten prüfen; Originalassets unverändert lassen und die
  Herkunft der generierten Varianten dokumentieren.

### Waffen- und Explosionseffekte

- [ ] Nach Renderer- und Lichtarbeiten Laser-/Waffenstrahlen, Treffer und
  Explosionen gestalterisch und technisch überarbeiten. Bestehende Effekt-
  Definitionen, Partikel, Materialien, Timing und dynamische Lichtanbindung
  inventarisieren; repräsentative Effekte vergleichen und anschließend gezielt
  modernisieren, ohne Originaleffekte oder Gameplay-Timing unbeabsichtigt zu
  verändern.
- [x] Beam- und Explosionseffektpfade inventarisiert; siehe
  [Effekt-Audit](beam-and-explosion-effects-audit.md). `flash_size` wird zwar
  vom Retail-Parser in den Beam-Datensatz übernommen und im Retail-Renderer
  bedingt zur Geometrieberechnung verwendet. LibreLancer wertet das Feld im
  Beam-Renderer noch nicht aus; Bedingung und Zeitpunkt müssen vor einer
  kompatiblen Umsetzung noch genauer verfolgt werden.

### Vollständige INI-Parameterabdeckung

- [ ] In einer späteren systematischen Prüfung die von der Client-Laufzeit
  geladenen INI-Dateien und ihre Parameter vom Parser über Datenmodelle und
  Zuordnung bis zum C#-Verbrauch verfolgen. Fehlende, ignorierte oder nur
  teilweise verwendete Werte inventarisieren und mit Beispielen und Tests
  planen.

### Modding-Erweiterung

- [ ] Nach Abschluss der INI-Abdeckung sinnvolle optionale Parameter für
  Verhalten oder Zustände identifizieren, die aktuell hardcodiert oder nicht
  ausgeführt werden. Kandidaten in passenden INIs mit rückwärtskompatiblen
  Defaults, klarer Zuständigkeit und Tests spezifizieren und nach Priorität
  umsetzen.

### NPC-Wegefindung (nach Renderer und dynamischen Lichtern)

- [ ] Nach Abschluss der Renderer- und Lichtarbeiten die NPC-Routenplanung
  und Laufzeitnavigation überarbeiten. Zuerst vorhandene Zielwahl, Wegpunkte,
  Sprungtore/-löcher, Hindernisvermeidung und Abbruch-/Neuberechnungspfade
  anhand reproduzierbarer Szenen prüfen. Danach festgestellte Fehler gezielt
  beheben und Verhalten in Einzel- sowie Gruppennavigation verifizieren.

### Flugphysik und Intro-Bewegungen

- [ ] Die ungewöhnlich hohe Geschwindigkeit der Intro-Sequenz reproduzieren
  und für repräsentative Schiffe Position, Wegstrecke, Dauer sowie daraus
  resultierende Geschwindigkeit dokumentieren. Intro-THN-Bewegungen und
  reguläre Schiffsphysik getrennt betrachten.
- [x] Den Laufzeitpfad von THN-Bewegung, `ShipPhysicsComponent`, Engine-Schub,
  linearer Reibung und Maximalgeschwindigkeit nachvollziehen. Feststellen, ob
  die beobachtete Geschwindigkeit aus Skript-Keyframes, Zeitschritt oder
  Flugparametern stammt. Für `m01a_fp7_01.thn` ist die Bewegung THN-kinematisch:
  `StartPathAnimationEvent` steuert `MotionPath` linear über die Ereignisdauer;
  `ShipPhysicsComponent` wird dabei nicht angewendet. Der isolierte Clientlauf
  bestätigt den Aufruf von `Act_CallThorn`. Die statische Weg-/Zeitmessung ist
  dokumentiert; ein visueller Framevergleich bleibt offen.
- [ ] Die gemessenen Werte mit lokalen Retail-DATA/EXE-Referenzen vergleichen
  und pro Schiffstyp Beschleunigung, Reisegeschwindigkeit und Höchsttempo
  ableiten. Keine globale Verlangsamung allein anhand des Intro-Eindrucks
  vornehmen.
- [ ] Bestätigte Abweichungen mit gezielten Daten-/Engineänderungen beheben;
  Änderungen an Enginecode als Client-Patch pflegen. Intro-Tempo und normale
  Steuerung anschließend separat im Client prüfen, einschließlich Beschleunigen,
  Abbremsen, Richtungswechsel und Szenenübergang.

Vorprüfung (2026-10-13): Die lokale Retail-Sequenz `m01a_fp7_01.thn` bewegt den
M01A-Transporter auf einem geraden 1.628,2-m-Pfad in 74 Sekunden, also mit rund
22 m/s. Das konfigurierte Transport-Engine-Modell ergibt aus `max_force=240000`
und insgesamt `linear_drag=3001` ein rechnerisches Endtempo von rund 80 m/s.
Die THN-Bewegung läuft kinematisch über `MotionPath`; ein Laufzeit-/Sichtvergleich
fehlt. Die statischen Werte bestätigen daher noch kein überhöhtes Spieltempo.

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
