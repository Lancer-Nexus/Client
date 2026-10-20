# Freelancer Data: Client-Umsetzungsplan

Dieser Plan ordnet die in [data-client-todo.md](data-client-todo.md) erfassten
Client-Arbeiten in eine ausführbare Reihenfolge ein. Die Quelldokumente im
[Data-Repository](../../Data/README.md) bleiben für Änderungen an INIs,
Assets, NAP-Paketen und Importwerkzeugen maßgeblich.

## Ziel und Arbeitsgrenzen

- Parser-, Engine-, Renderer- und Wiedergabeänderungen erfolgen im Client als
  fokussierte Patches unter `patches/`; die Patchserie und eine frische
  Overlay-Rekonstruktion gehören zu jeder Codeänderung.
- Freelancer-Daten und Originalassets bleiben lokale Testeingaben. Sie werden
  nicht in Client-Git oder synthetische Testfixtures kopiert.
- Data-Änderungen (INI-Werte, Asset-Ersetzungen, Video-Transkodierung,
  NAP-Inhalte und deren Generator) werden als getrennte Data-Arbeit geführt.
- Ein erfolgreiches Parsen ist kein Nachweis für korrekte Darstellung oder
  Wiedergabe. Jede Datenfamilie braucht einen gezielten Laufzeitnachweis.

## Phasen und Reihenfolge

### Phase 0 – Testbasis und offene Befunde festhalten

- [x] Lokale Referenzinstallation read-only erfasst: `DATA/`, `EXE/` und
  `Freelancer.exe` sind vorhanden; die EXE ist ein 32-Bit-PE. Ein belastbarer
  Produkt-/Buildstand ist aus den verfügbaren lokalen Metadaten nicht
  ablesbar. Data-SDK-Quellstand der Sichtung: Commit `d16fe9d` (2026-10-11).
  Keine Originaldateien oder Zugangsdaten wurden kopiert oder veröffentlicht.
- [x] Repräsentative Eingabefälle aus den Data-TODOs festgelegt: `Br01CornwallRockAsteroidField.ini`,
  `Omega13.ini` samt Ringsystem, `Li01BadlandsNebula.ini`, `Br0104Bar.ini`, die
  drei `Startup`-Movie-Einträge in der lokalen `freelancer.ini` sowie synthetische
  Ogg/Vorbis-, FLAC- und Ogg-FLAC-Musik. Auswahl dokumentiert; der Bar-Raum-
  Laufzeitnachweis steht in Phase 3. Die visuellen und Video-Laufzeitprüfungen
  bleiben offen.
- [x] Data-Fehlzielzählung mit `Data/tools/check_ini_references.py`
  reproduziert und gegengeprüft: ein read-only Lauf gegen die lokale CD-DATA
  (Build-Namensregeln plus SDK-INI-Overlay) meldet 50 Zielpfade / 83
  INI-Zeilen. Die 48 Tabellenziele in `Data/missing.md` stimmen damit überein;
  zwei zusätzliche Detailmaps sind als HD-Dateien in `Data/tools/hd_overrides.txt`
  deklariert, aber im lokalen CD-Baum nicht vorhanden. Bei der Phase-0-Sichtung
  enthielt der Data-NAP nur `.gitkeep`; Phase 3 synchronisierte später 1252 SDK-
  INIs, aber keine Binärassets. Die physische HD-Präsenz bleibt offen.
  Absolute Entwicklerpfade werden vom Generator ausgeschlossen. Keine fehlende
  Ressource wird automatisch dem Client zugerechnet.
- [ ] Den Abgleich nach Aufbau des vollständigen NAP mit `--nap NAP`
  wiederholen und die beiden HD-Detailmaps physisch bestätigen; der aktuelle
  NAP enthält synchronisierte INIs, aber noch keine Audio-/Modell-/Texturassets.
- [x] Linux-Debugclient aus dem aktuellen Arbeitsbaum mit temporärer
  Konfiguration gegen die lokale Freelancer-Installation gestartet. Innerhalb
  von 3,88 s wurden `Game: Finished loading game data` und `UI: Interface
  loaded` erreicht; der 17-s-Startmenülauf blieb ohne Error/Exception und wurde
  kontrolliert beendet. Die 913-zeilige Logdatei enthält 77 Parserwarnungen,
  u. a. unbekannte `explosion_impulse`-/`particle_effect`-Felder in
  `AsteroidArch.ini` und einen `:gap`-Eintrag in einer Room-INI. Das sind
  Befunde für Phase 1, keine bestätigten Clientfehler. Konfiguration und Log
  liegen lokal unter `/tmp/lancer-phase0-client-baseline-20261012/`; die
  Originalinstallation blieb unverändert.
- [x] Separater Debug-Lauf mit `--new-game` betrat `FP7_system` und renderte
  Schiffe, Station, Sterne und Nebel aus der lokalen Retail-DATA. Das Log
  erreichte `Game: Finished loading game data`, `UI: Interface loaded` und
  `Game: Entering system FP7_system`; in 1.342 Logzeilen fanden sich 108
  Warnungen und keine Error-, Fatal- oder Exception-Zeilen. Der Client wurde
  kontrolliert beendet. Screenshot und Log liegen lokal unter
  `/tmp/lancer-solar-runtime-20261012/window.png` und `stdout.log`. Dieser
  Standalone-Lauf belegt den Renderpfad für diese Retail-Szene, aber weder
  HD-/NAP-Inhalte noch registrierte Clusterwelten oder einen Vergleich mit dem
  Originalspiel.
- [x] Ein weiterer isolierter Debug-Lauf betrat `br01` am Zentrum des Cornwall-
  Asteroidenfelds (`25319, 0, 60104`). Dafür wurde ausschließlich eine
  temporäre `EXE/newplayer.fl`-Kopie angepasst (`system=br01`,
  `Mission=No_Mission`); Retail-DATA blieb unverändert. Der Client meldete
  `Initing 95asteroids`, `Entering system br01` und `Not loading mission`; der
  Screenshot `/tmp/lancer-br01-asteroid-runtime-20261012/window-clean.png`
  zeigt die Asteroiden im freien Raum. Das 1.064-zeilige Log hat 93 Warnungen,
  aber keine Error-, Fatal- oder Exception-Zeilen. Das ist ein Retail-
  Renderbaseline, kein HD-/NAP- oder Originalspielvergleich.
- [x] `StartupMovies`-Verwendung im Client suchen: `FreelancerIni` parst
  `movie_file` in eine Liste, im aktuellen Client-Quellbaum gibt es außer
  Deklaration, Initialisierung und Parserzugriff keinen Verbraucher oder
  Wiedergabeaufruf. Der tatsächliche Playback-Pfad ist damit derzeit nicht
  angebunden.

**Abnahme:** Für jeden Fall ist klar, welche Daten geladen werden, wie der
Test gestartet wird und woran Erfolg oder Fehler erkannt wird. Ungeklärte
Asset- und Laufzeitpfade bleiben ausdrücklich offen. Die Bestandszählung oben
ist eine Klassifikation der vorhandenen Markdown-Liste, noch kein unabhängig
reproduzierter Inventarlauf.

### Phase 1 – Parser- und Datenflussprüfung

**Erster Asteroidenbefund aus dem Stock-DATA-Start (2026-10-12):** Der Client
meldete 77 Parserwarnungen, darunter `explosion_impulse` in `[AsteroidMine]`
und `particle_effect` in `[DynamicAsteroid]`. `Schema/Solar/Asteroid.cs` hat
keinen Eintrag für `explosion_impulse`; `Schema/Solar/DynamicAsteroid.cs` hat
keinen Eintrag für `particle_effect`. `GameItemDb` überträgt bei dynamischen
Asteroiden Modell und Explosion, aber keinen Partikeleffekt. Das belegt eine
nicht abgebildete Datenstrecke, noch nicht deren sichtbare Auswirkung. Vor
Änderungen ist das Verhalten im Originalspiel zu prüfen. Der Start verwendete
die lokale Installation ohne SDK/NAP-Overlay.

**Retail-/SDK-Abgleich und Binärspur (2026-10-12):** Beide `AsteroidArch.ini`
enthalten dieselben drei Minen mit `explosion_impulse = 1000, 100, 1000` und
dieselben drei dynamischen Asteroiden mit den Partikeleffekten
`gf_blsmallasteroid` bzw. zweimal `gf_dynamiclavaast`. Ein Ghidra-String-Xref
in Retail-`Freelancer.exe` (SHA-256 `ea4cb797…6d1fe9`) führt zu
`FUN_00408cd0` bei `0x00408cd0`: der Parser liest `particle_effect`, bildet aus
dem Wert eine ID und speichert sie bei Objekt-Offset `+0x54`, dazu ein
Vorhanden-Flag bei `+0x58`. Der nachgelagerte Verbraucher und der Zeitpunkt der
Effektausgabe sind damit nicht identifiziert. Für `explosion_impulse` gibt es
keinen direkten Stringtreffer in den installierten EXE/DLL-Dateien; ein
ignorierter Schlüssel ist damit nicht bewiesen. Der Originalspiel-Vergleich
für Minenexplosion und dynamische Asteroidenzerstörung bleibt Voraussetzung
für jede Implementierung.

- [x] Asteroidenfelder vom Schema über `GameItemDb` bis Renderer und
  `AsteroidFieldComponent` verfolgt; konkrete Verbraucher und Lücken stehen
  im Befund unten.
- [x] Nebel- und planetare Ringdateien vom Schema über `GameItemDb` bis zum
  Renderer verfolgt; konkrete Verbraucher und Lücken stehen im Befund unten.
- [x] Schwarze-Loch-Dateien und ihre Referenzen in SDK-Systemdaten sowie
  Client-Parser/Renderer geprüft; ein aktiver Daten- oder Renderpfad ist nicht
  belegt (siehe Befund unten).
- [x] SolarArch-/StarArch-Felder für Modelle, Planetengröße und Sternvisuals
  vom Schema über `GameItemDb` bis Modell-/Rendererverbrauchern verfolgt.
- [x] Schlüssel-zu-Code-Abgleich für Solar- und Sternarchetypen durchgeführt;
  übertragene und nicht angebundene Werte stehen im Befund unten.
- [ ] Die sichtbare Wirkung der HD-Planeten-/Sternwerte im repräsentativen
  HD-/NAP-Clientlauf prüfen. Parser- und Datenmodellpfade allein reichen dafür
  nicht aus.
- [ ] Die offene Laufzeitprüfung in repräsentativen HD-/NAP-Systemen fortführen;
  der FP7-Retail-Standalone-Lauf ist nur ein Baseline-Rendernachweis und deckt
  keine der dokumentierten HD-Abweichungen ab.
- [x] Asteroiden-HD-Felder im Code abgeglichen: `fill_dist`,
  `empty_cube_frequency`, Feldfarben, Billboard-`start_dist` und
  `fade_dist_percent` erreichen Laufzeitverbraucher. `Band.render_parts` und
  `Band.fade` werden geparst und zwischengespeichert, aber vom Renderer nicht
  verwendet. Weitere ungeverbrauchte Felder und das Exclusion-Verhalten sind
  im nachfolgenden Befund festgehalten; visuelle Abnahme bleibt offen.
- [x] Parser-Inventar für `Data/SDK/solar/SolarArch.ini` erstellt: alle 28
  unterschiedlichen Schlüssel in `[Solar]` besitzen einen `[Entry]`- oder
  `[EntryHandler]`-Eintrag in `Schema/Archetype.cs`; unbekannte Schlüssel wurden
  nicht gefunden. Das bestätigt Parserdeklarationen, nicht die Laufzeitnutzung
  oder die korrekte Darstellung der Werte.

**Asteroidenfeld-Codepfad (2026-10-12):** `Schema/Universe/AsteroidField.cs`
liest `Field`, `Band`, `ExclusionBand`, `AsteroidBillboards` und
`ExclusionZones`. `GameItemDb.GetAsteroidField()` überträgt `Field.CubeSize`,
`FillDist`, Leerwürfelhäufigkeit und Feldfarben; der Renderer verwendet diese
Werte für Würfelraster, Dichte, Distanz und Tönung. Billboard-`Count`,
`StartDist`, `FadeDistPercent`, `Shape`, `Size` und `ColorShift` werden bis zur
Billboard-Darstellung weitergereicht. `Band.Shape`, `Height`, `OffsetDist`,
`TextureAspect` und `ColorShift` beeinflussen den gezeichneten Ringzylinder.

Der Codepfad zeigt mögliche HD-Abweichungen, deren sichtbare Wirkung noch mit
dem Originalspiel verglichen werden muss. Im Data-SDK-Stand `d16fe9d`
(2026-10-11) kommen `Field.max_alpha` achtmal und `contains_fog_zone` einmal
vor; keines wird ins Laufzeitmodell übernommen. `ExclusionBand` ist in acht SDK
Dateien belegt, wird nach der Schema-Deserialisierung aber nicht weiter
referenziert. `Band.Zone`, `AmbientIntensity`, `CullMode` und `VertIncrease`
werden nicht ins Laufzeitmodell übernommen. `RenderParts` und `Fade` werden
zwar übertragen, aber nicht vom Renderer verwendet (der Zylinder zeichnet
konstant 20 Seiten). `AsteroidBillboards.AmbientIntensity` ist in 95 SDK-Dateien
gesetzt, wird aber nicht übernommen.

Die Exclusion-Metadaten `ExcludeBillboards`, `ExcludeDynamicAsteroids`,
`EmptyCubeFrequency` und `BillboardCount` landen im Modell, werden aber nicht
als Steuerwerte verwendet. Der Renderer schließt statische Würfel für jede
Exclusion-Zone aus, ohne `ExcludeBillboards` abzufragen; Billboards prüfen die
Zonen nicht. Die dynamische Asteroidenkomponente verhindert Spawns in jeder
Exclusion-Zone, ohne `ExcludeDynamicAsteroids` abzufragen. Das beschreibt das
Implementierungsverhalten, nicht die beabsichtigte Freelancer-Semantik.

**HD-Asteroiden-INI-Pfade (2026-10-12):** Ein read-only Abgleich aller
`[Asteroids] file`-Einträge in `Data/SDK/universe/systems/**/*.ini` fand 210
Verweise; alle 210 passen case-insensitiv zu Dateien in
`Data/SDK/solar/asteroids/`. BR01 verweist auf
`solar\asteroids\Br01CornwallRockAsteroidField.ini`, und genau diese Datei
existiert im SDK. `sync_inis_to_nap.py --dry-run` ordnet beide INIs dem
`World`-Container mit erhaltenem relativem Pfad zu. Es gibt damit keinen
statischen Dateinamen- oder Klassenpfadbruch. Der NAP-Container enthält lokal
noch keine synchronisierten Dateien; ein vollständiger Paketlauf und die
visuelle HD-Prüfung bleiben offen.

**Fehlende Asteroidentextur `roids.txm` (2026-10-12):** Die SDK-`Shapes.ini`
enthält sieben Verweise auf die nicht vorhandene Textur. Ein normalisierter
Pfadscan fand jedoch weder in Retail-DATA noch in den SDK-System- und
Asteroiden-INIs einen Verweis auf `solar/asteroids/Shapes.ini`; auch die 15
darin deklarierten Shape-Namen werden außerhalb dieser Datei nicht verwendet.
Der aktuelle Datenbestand erreicht den defekten Pfad deshalb nicht. Im
Clientcode würde eine später aktivierte Shape-Definition die Textur über
`FindTexture()` als `null` erhalten; `Billboards.DrawTri()` berechnet danach
einen Hash über die fehlende Textur und kann mit einer Nullreferenz abbrechen.
Die BR01-Laufzeitbaseline lädt `RockShapes.ini` und deckt diesen Fall nicht ab.
Eine Asset-Ersetzung oder INI-Änderung bleibt eine Data-Entscheidung.

**Nebel- und planetare Ringpfade (2026-10-12):** `Schema/Universe/Nebula.cs`
liest Fog, Exterior, Clouds, Lightning, Lights und Exclusion-Zonen. `GameItemDb.GetNebula()`
überträgt Fog-Werte, erste `[NebulaLight]`-Daten, erste `[Clouds]`-Daten,
Außenpuffs, Lightning-Parameter und Exclusion-Shells. `NebulaRenderer` nutzt
Fog-Übergänge, Innen-/Außenpuffs, Lightning und Ausschlusszonen; `SunRenderer`
verwendet die Burnthrough-Intensität und den Skalierer.

Mehrere gültig geparste Felder sind nicht angebunden: `plane_slices` und
`equator_bias` stehen im Data-SDK in jeweils 59 Nebeldateien, werden nicht ins
Laufzeitmodell kopiert; der Renderer erzeugt Außenpuffs stattdessen auf drei
fest codierten Höhen. `ambient_intensity` und `intensity_increase` in
`[DynamicLightning]` kommen jeweils 20-mal vor, werden ebenfalls nicht
übertragen. `GameItemDb` liest nur `NebulaLights[0]`; zwei SDK-Dateien enthalten
mehrere `[NebulaLight]`-Abschnitte (`Hi01SiniestreEdgeNebula.ini`,
`Bw06KunashirEdgeNebula.ini`), sodass spätere Abschnitte nicht im Modell landen.
Das sind Codepfad-Lücken; ihre sichtbare Wirkung ist noch offen.

Planetare `ring = zone, solar\rings\*.ini`-Einträge werden im
`SystemObject`-Schema geparst und von `GameItemDb` als `RingZone`/`RingFile`
übernommen. Im Client-Quellbaum enden diese Werte danach im Datenmodell; ein
Renderer- oder Ladeverbraucher für die Ringdatei wurde nicht gefunden. Davon
getrennt unterstützt `Zone.ShapeKind.Ring` die Geometrie von Ringzonen für
Punkt-/Abstandsabfragen; diese Zonenform rendert nicht automatisch das visuelle
Ringasset. Der visuelle Originalspielvergleich bleibt erforderlich.

**Schwarze-Loch-Dateien (2026-10-12):** Ein Abgleich der lokalen,
read-only Retail-Datei `DATA/SOLAR/BLACKHOLE/bh.ini` mit
`Data/SDK/solar/blackhole/Bh.ini` zeigt, dass die SDK-Variante `[Sound]`,
`[FadeChunks]` und `[CloseChunks]` entfernt und die `lava_wisp`-Formen durch
`rock_wisp` ersetzt. In den SDK-System-INI-Dateien wurde keine Referenz auf die
Black-Hole-Konfiguration gefunden. Der Client besitzt keinen dedizierten
Black-Hole-Parser oder -Renderer. Die generischen `Nebula`-Schemas decken die
Black-Hole-Schlüssel wie `ball_scale`, `flash`, `puff_density`, `puff_size`,
`puff_opacity` und `Fog.opacity` nicht ab. Damit ist für diese Dateien kein
aktiver Client-Datenpfad nachgewiesen; eine sichtbare Auswirkung oder eine
Entscheidung zu den entfernten Retail-Abschnitten lässt sich daraus nicht
ableiten. Der Vergleich im Originalspiel bleibt offen.

**Solar-, Planeten- und Sternpfade (2026-10-12):** `SolararchIni` lädt
Solar-Archetypen mit `material_library`, `da_archetype` und `solar_radius`;
`GameItemDb.InitArchetypes()` überträgt diese in das Datenmodell. Der generische
`ResolveDrawable()` übergibt Modell und Bibliotheken an `DataPath()`. Dieses
prüft jeden angegebenen Pfad mit `VFS.FileExists()`; fehlt er, wird ein Fehler
geloggt und `null` geliefert. Es gibt keinen automatischen Basename-Fallback
und keine Flatten-/Aliasregel. Das Systemobjekt löst den Archetyp auf und
`GameObject` lädt dessen Modell. Für die von Data auf flache Pfade
umgestellten `.mat`-/`.txm`-Dateien gibt es keine Client-Sonderbehandlung: die
INI-Referenz und der tatsächliche VFS-/NAP-Pfad müssen exakt übereinstimmen.
`Data/TODO-solar.md` meldet 932 aufgelöste Asset-Verweise. Ein aktueller
read-only Lauf von `Data/tools/check_ini_references.py --data-root` gegen den
lokalen Retail-DATA-Baum reproduziert 932 Verweise aus `SolarArch.ini`, findet
dort aber zwei Ziele in 21 Fundstellen nicht: `solar/planets/detailmap_gas.txm`
(9) und `solar/planets/detailmap_ice.txm` (12). Der Checker bildet die
Retail-Umbenennungen aus `build_nap.py` ab; sein `--data-root`-Modus enthält
keine HD-Dateien, die nur im NAP liegen. Daher beweist dieser Befund weder,
dass die zwei Ziele im finalen HD-NAP fehlen, noch bestätigt er die vollständige
932er-Paketabdeckung. Der aktuelle NAP-Bestand ist leer bis auf `.gitkeep`;
der vollständige NAP-Referenzlauf und ein visueller HD-Test stehen aus.
`solar_radius` wird für Navmap-Icons und den Besuchsradius verwendet; das
sichtbare Planetenmaß stammt vom geladenen Modell.

Ein statischer Abgleich der 28 unterschiedlichen `[Solar]`-Schlüssel aus dem
aktuellen HD-`SolarArch.ini` mit den `[Entry]`- und `[EntryHandler]`-
Deklarationen von `Schema.Archetype` ergab vollständige Parserdeklaration. Das
schließt ungenutzte Werte oder Laufzeitabweichungen nicht aus.

Bei Systemobjekten wird `atmosphere_range` für `GameWorld.InAtmosphere()`-
Abfragen übernommen. `burn_color` und `ambient_color` werden zwar ins
Datenmodell kopiert, haben im untersuchten Spielquellbaum aber keinen
Verbraucher; die Datenmodellklasse kennzeichnet diese Eigenschaften als noch
nicht im Spiel verwendet.

`StarArchIni` parst `lens_flare`, `lens_glow`, `intensity_fade_in/out` und
`zone_occlusion_fade_in/out`. `GameItemDb.InitStars()` übernimmt zusätzlich die
referenzierte `LensGlow`-Form, Farbe, Radius und deren
`glow_fade_in_seconds`/`glow_fade_out_seconds`. `SunRenderer` zeichnet den
additiven Lens-Glow und blendet ihn beim Eintritt in beziehungsweise Austritt
aus dem Kamerafrustum ein oder aus. Dieser Fade berücksichtigt keine
Geometrie-Occlusion. `lens_flare` und die Stern-Intensitäts-/Zonen-Fades werden
weiterhin nur geparst; der visuelle Abgleich mit dem Originalspiel bleibt offen.

**Schlüssel-zu-Laufzeit-Abgleich SolarArch/StarArch (2026-10-12):** Die
HD-`SolarArch.ini` enthält 321 `[Solar]`-Abschnitte mit 28 unterschiedlichen
Schlüsseln; `StarArch.ini` enthält 38 `[Star]`-Abschnitte mit 11 Schlüsseln.
Der Abgleich der Schemafelder mit `GameItemDb.InitArchetypes()` und den
nachgelagerten Lesern bestätigt für Solararchetypen die Übernahme von
`nickname`, `type`, `loadout`, `shape_name`, `solar_radius`,
`phantom_physics`, `docking_sphere`, `open_anim`, `lodranges`,
`material_library`, `DA_archetype`, `envmap_material`, `hit_pts` und
`destructible`. Modell und Materialien laufen über `ResolveDrawable()` und den
exakten VFS-Pfad; `GameObject` lädt das Modell und setzt Envmap, Physik und
separable Teile. `GameWorld` übernimmt LOD-Werte; `ServerWorld` verwendet
Solar-Loadouts und Trefferpunkte. `solar_radius` steuert Navmap-Icongröße und
Besuchsradius, nicht die sichtbare Modellgröße des Planeten.

Die folgenden 14 weiteren Solar-Schlüssel sind zwar im gemeinsamen
`Schema.Archetype` deklariert, werden im Solar-Konvertierungspfad aber nicht ins
Laufzeitmodell übertragen: `ids_name`, `ids_info`, `mass`, `explosion_arch`,
`open_sound`, `close_sound`, `docking_camera`, `jump_out_hp`, `fuse`,
`shield_link`, `surface_hit_effects`, `animated_textures`, `distance_render` und
`nomad`. Einige Felder haben getrennte Verbraucher bei Schiffen oder anderen
Solar-Schemas; ihre Deklaration allein belegt daher keine Unterstützung für
Solararchetypen.

Für Sterne übernimmt `GameItemDb.InitStars()` 6 der 11 Schlüssel:
`nickname`, `radius`, `star_glow`, `star_center`, `spines` und die Referenz
`lens_glow`. `SunRenderer` zeichnet Radius, Glow, Center, Spines und den
Lens-Glow. Die fünf Schlüssel `lens_flare`, `intensity_fade_in`,
`intensity_fade_out`, `zone_occlusion_fade_in` und `zone_occlusion_fade_out`
werden geparst, aber nicht im Client-Spielpfad verwendet. Die Fade-Zeiten des
separaten `[LensGlow]`-Abschnitts sind angebunden, aber noch nicht visuell gegen
das Original abgenommen. Die aktuelle NAP-Referenz liegt lokal nur als
`.gitkeep` vor; ein neuer Paket-/Renderlauf ist daher offen.

- [ ] Fehlende und ungültige optionale Ressourcenfälle erfassen. Parser- und
  Regressionstests nur für bestätigte Client-Grenzen ergänzen.
- [ ] Vor Änderungen an Musik- oder Videoformaten den aktuellen Loader,
  Decoder, Plattformpfad, Loop-/Synchronisationsverhalten und Fehlerpfad
  anhand des Codes belegen.

**Abnahme:** Jedes untersuchte Feld hat einen belegten Verbraucher oder ist
als ungenutzt/offen markiert. Parserlücken haben einen kleinen reproduzierbaren
Testfall.

### Phase 2 – Visuelle Datenlaufzeit: Asteroiden, Solar, Nebel und Ringe

- [x] Retail-Baseline im BR01-Cornwall-Asteroidenfeld aufgenommen: Client-Build,
  Systemeintritt, Asteroidenrenderer und freier Szenen-Screenshot sind unter
  `/tmp/lancer-br01-asteroid-runtime-20261012/` belegt. HD-Änderungen und
  Vergleich mit dem Originalspiel bleiben offen.
- [x] BR01 Cornwall mit Retail- und HD-SDK-Feld-INI im selben Client-Build
  gestartet. Beide Läufe erreichten das System ohne Laufzeitfehler; Logs und
  Bilder liegen unter `/tmp/lancer-proton-freelancer-asteroid-20261012/`.
  `fill_dist` (1650 → 3300) und Billboard-`start_dist` (1000 → 2000) werden im
  Client-Renderer verwendet; Würfelhäufigkeit und Farben sind unverändert.
  Das SDK-`RockShapes.ini` wurde im Testbaum unter dem referenzierten Namen
  bereitgestellt. Für den Originalrenderer scheiterten Proton 7.0 und Wine
  10.20 vor der Szene an der nicht erkannten 3D-Karte; DXVK scheiterte an
  `VK_KHR_surface`. Ein Proton-9-Beta-Lauf auf dem Intel-HD4600-Desktop rendert
  inzwischen das animierte Titelmenü samt 3D-Hintergrund aus einer vollständigen
  temporären Retail-Kopie (`DATA`, `EXE`, `DLLS`). Die Retail-Installation blieb
  unverändert. XTest- und isolierte uinput-Eingaben konnten „New Game“ nicht
  auslösen, daher gibt es weiterhin keinen Original-Szenenvergleich. Beleg:
  `/tmp/lancer-proton-freelancer-asteroid-20261012/`.
  Im Wiederholungslauf ließ sich der 3D-Warnhinweis per Win32 `BM_CLICK` auf
  `IDYES` schließen; Win32-SendInput-/PostMessage-Klicks lösten danach keinen
  Szenenwechsel aus. Die X11-Aufnahme blieb schwarz, obwohl das Spiel weitere
  Renderwarnungen protokollierte. Dieser Lauf belegt daher keinen sichtbaren
  Menü- oder Szenenzustand.
  Ein Test mit `fullscreen = 0` und `size = 1024,768` ergab ein zentriertes
  1024×768-Fenster, aber weiterhin nur eine schwarze X11-Aufnahme. Die
  Testkopie-Konfiguration wurde auf den Retail-Stand zurückgesetzt.
- [ ] Mit der Phase-0-Basis Asteroidenfelder und -formen sowie Feldgrenzen in
  repräsentativen Systemen prüfen: Laden, Abstände, Sichtbarkeit, Dichte,
  Farben und Stabilität.
- [ ] Omega 13 sowie betroffene Ringsysteme prüfen: Schwarzes Loch,
  `rock_wisp`-Puffs, Ringformen/-texturen, Sichtbarkeit, Speicherbedarf und
  Laufzeitstabilität.
- [ ] Planeten mit flachen Materialpfaden, Sterne, Sonnen, Nebel und
  Asteroiden-/Sonnen-Loadouts prüfen; Material- und Texturauflösung im Log
  erfassen.
- [ ] Auswirkungen entfernter `[Sound]`, `[FadeChunks]` und `[CloseChunks]`
  datengetrieben vergleichen. Dauerhafte INI-Entscheidungen werden danach im
  Data-Repository getroffen.
- [ ] Fehlende `solar/asteroids/roids.txm`-Referenzen mit dem Data-Team bzw.
  Data-Inventar klären; Ersatztextur oder SDK-Verweis nicht im Client
  vorwegnehmen.

**Abnahme:** Repräsentative Fälle haben Laufzeitlogs und Bildnachweise; jeder
Fehler ist einem Datenproblem, einer bestätigten Client-Lücke oder einem noch
offenen Befund zugeordnet. Danach können erforderliche Engine-Fixes als
separate Client-Patches umgesetzt werden.

### Phase 2a – Bestätigte Parser- und Renderlücken umsetzen

Diese Phase setzt die Befunde aus Phase 1 und 2 tatsächlich im Client um.
Ein Punkt wird hier nur begonnen, wenn ein reproduzierbarer Laufzeitbefund
zeigt, dass die Daten gültig sind und Parser, Engine oder Renderer sie falsch
oder gar nicht verarbeiten. Pro Befund gilt: Reproduktion festhalten, kleinsten
Patch erstellen, Regression ergänzen, betroffene Konfiguration bauen und den
ursprünglichen Laufzeitfall wiederholen.

- [ ] **Asteroiden:** fehlende Verwendung von `shape`, `exclusion`, `zone`,
  `render_parts` oder `fade` ergänzen, wenn die Prüfung eine konkrete
  Client-Lücke belegt; Abstände, Dichte, Farben und Feldgrenzen danach im
  selben repräsentativen System erneut prüfen.
- [ ] **Schwarze Löcher und Ringe:** bestätigte Lücken bei Chunk-/Sound-
  Verhalten, `rock_wisp`, Ringformen oder Texturauflösung gezielt beheben und
  Regressionen für betroffene Felder bzw. optionale Ressourcen ergänzen.
- [ ] **Solar, Planeten und Nebel:** bestätigte Fehler bei Material- und
  Detailmap-Pfaden, HD-Solarfeldern, Nebeln, Sternen, Sonnen oder Loadouts
  korrigieren und die betroffenen Renderfälle erneut aufnehmen.
- [ ] **Gemeinsame Asset-Auflösung:** eine Client-Korrektur für fehlende
  `roids.txm`-Verweise nur umsetzen, wenn Data-Inventar und Laufzeittest eine
  Engine-Lücke statt eines fehlenden oder ungültigen Datenassets belegen.
- [ ] Jeden bestätigten Fix als fokussierten Patch in `patches/` pflegen, in
  `patches/series` registrieren und die vollständige Patchserie frisch
  rekonstruieren und verifizieren.

**Abnahme:** Der ursprüngliche Fehler ist mit demselben lokalen Datensatz
behoben, passende Regressionen bestehen und Data-Quelldateien bleiben
unverändert. Bei Datenursachen wird stattdessen ein verlinkter Data-Auftrag
erfasst.

### Phase 2b – Gesichts- und Charaktermaterialien

- [ ] Für ein repräsentatives Charaktermodell NAP-Texturen, UV-Sets,
  MAT-Bindungen sowie vorhandene Normal- und Roughness-Daten zuordnen.
- [ ] Eine optionale Image-to-Image-LLM-Variante mit weniger plastikartiger
  Hautoberfläche erstellen. Passende Normal-/Roughness-Maps müssen aus derselben
  Variante abgeleitet oder konsistent dazu erzeugt werden; keine Originaldatei
  überschreiben. Eingabe, Prompt/Modell und erzeugte Dateien dokumentieren.
- [ ] Original und Variante mit unverändertem Modell, UVs, Beleuchtung und
  Kamerawinkel im NAP-only-Client vergleichen. Gesichtsidentität, UV-Nähte,
  Hautdetails und Wirkung in Dialog-/Raumansichten prüfen.

**Abnahme:** Original und neue Assetvariante sind reproduzierbar per NAP
umschaltbar, ein kontrolliertes Bildpaar liegt vor und die zugehörigen
Materialkanäle passen sichtbar zusammen.

### Phase 3 – Raum-Musik-Playlists

- [x] Rückwärtskompatible Syntax `music = track_a, track_b, track_c`
  eingeführt; Einzelwerte und das bestehende `oneshot`-Suffix bleiben erhalten.
- [x] Zufällige Auswahl ohne direkte Wiederholung implementiert. Playlisttracks
  spielen einmal und der nächste Track startet nach Ende des Streams. Ein
  einzelner Track bleibt geloopt; `oneshot` auf einer Playlist spielt einen
  zufälligen Track genau einmal. Der Wechsel ist trackende- und nicht
  timerbasiert.
- [x] Parser- und Auswahltests für Einzeltrack, Playlist, `oneshot`, leere
  Auswahl und Wiederholungsvermeidung ergänzt; vier gezielte Tests liefen im
  aktuellen Client-Overlay mit Patch 2171 erfolgreich.
- [x] Wiedergabe und Trackwechsel in `Br0104Bar` mit dem lokalen Retail-
  Datenbestand im Debug-Client geprüft. Dafür wurde eine isolierte Kopie
  verwendet: `Br01_04_Base` startete direkt in `Br01_04_Bar`; die Raum-INI
  verwies auf zwei synthetische Ogg-Tracks von 0,7 s und 0,9 s. Das Log meldet
  den Daten- und UI-Start sowie zehn EOF-Wechsel zwischen beiden Tracks ohne
  Error-, Fatal- oder Exception-Einträge. OpenAL lief über den Null-Treiber;
  dies belegt Parser, Streamende und Playliststeuerung, nicht die akustische
  Qualität der Originaltracks.
- [x] Nach der Client-Abnahme 131 generische Bar-INIs im Data-Repository auf
  `music_bar_generic01` bis `music_bar_generic10` umgestellt und SDK-INIs mit
  `tools/sync_inis_to_nap.py` synchronisiert. Nach Erhalt der CRLF-Zeilenenden
  wurden 131 Ziele aktualisiert und 1121 blieben unverändert; der finale Dry-Run
  meldete 1252 unveränderte INIs. Alle 131 `NAP/World`-Dateien sind byte-
  identisch.

**Abnahme:** Bestehende Einzeltrackräume bleiben kompatibel; Parser- und
Auswahltests sowie der Bar-Lauf belegen Playlist-Auswahl und Wechsel. Ein
separater Hörtest mit Original-Bar-Musik ist weiterhin offen.

### Phase 4 – Startup-Videos und Zielplattformen

- [x] Den `StartupMovies`-Pfad geprüft: `FreelancerIni` liest `movie_file`,
  aber im Client-Quellbaum gibt es keinen Verbraucher und keinen
  Wiedergabeaufruf. Startup-Movies werden derzeit nicht abgespielt.
- [x] Decoder-Codebestand geprüft: `src` enthält nur das Parsen von
  `StartupMovies`/`movie_file`; ein Video-Decoder und ein Wiedergabepfad fehlen.
  Die HD-Data-Dokumentation nennt WMV9/VC-1 mit WMA als Bestandsformat.
- [ ] Einen Decoder und unterstützte Formate für jede Zielplattform auswählen
  und nach Anbindung mit einem echten Client-Lauf verifizieren.
- [ ] Mit einem synthetischen oder anderweitig rechtmäßig bereitgestellten
  Testclip Bild, Ton, Synchronität, Abbruch, Fehler und fehlenden Decoder
  prüfen. Keine Originalvideos als Testfixture committen.
- [ ] MP4/H.264/AAC nur nach erfolgreichem Test auf den Zielplattformen als
  Clientformat freigeben. WMV bleibt bis dahin im Data-Bestand.
- [ ] Video-Upscaling/-Konvertierung, NAP-Ablage und Schutz vor Überschreiben
  bleiben getrennte Data-Arbeiten.

**Abnahme:** Der Pfad von der Konfiguration bis zur Wiedergabe ist belegt;
Formatfreigaben enthalten Plattform- und Testnachweis.

### Phase 4a – Startup-Video-Wiedergabe umsetzen (bei bestätigtem Bedarf)

- [ ] Nach der Entscheidung aus Phase 4 einen Playback-Einstieg für
  `StartupMovies` im bestehenden Client-Start-/UI-Lifecycle integrieren.
  Überspringen, Abbruch, fehlende Datei und fehlender Decoder müssen
  kontrolliert zum normalen Spielstart führen.
- [ ] Einen Decoderpfad wählen, der auf jeder unterstützten Zielplattform
  verfügbar und wartbar ist. Implementierung erst nach erfolgreichem
  synthetischem Cliptest für Bild, Ton, Synchronität, Abbruch und Fehlerpfad.
- [ ] Den Client-Fix mit Regressionstests als Patchserie pflegen und auf den
  freigegebenen Plattformen im Client abnehmen. Ein alternatives
  Spielvideoformat wird erst danach im Data-Repository freigegeben.

**Abnahme:** Konfigurierte Startup-Movies werden auf den freigegebenen
Plattformen abgespielt; ein fehlender oder nicht decodierbarer Film verhindert
den Spielstart nicht. Upscaling, Konvertierung und NAP-Inhalte bleiben
getrennte Data-Arbeiten.

### Phase 5 – Ogg-/FLAC-Musik und Schleifen

- [x] Vorhandenen Audiofluss geprüft: `AudioEntry.File` wird über den VFS
  geöffnet und durch `lancerdecode` anhand der Dateisignatur decodiert;
  Vorbis und FLAC benötigen keine neue INI-Syntax oder Audio-Aliase.
- [x] Wiedergabeart abgegrenzt: Musik wird gestreamt und benötigt beim
  Loopen einen Decoderreset; Einzelsounds werden vollständig nach PCM
  decodiert und durch OpenAL-Schleifenwiedergabe wiederholt.
- [x] Den Vorbis-Reset nach EOF korrigiert: Decoder schließen und den Ogg-
  Vorbisstream ab Byte 0 neu öffnen; bei fehlgeschlagenem Reset liefert der
  Stream kontrolliert EOF. Die Änderung liegt als Patch 2172 im
  `lancerdecode`-Ziel der Patchserie.
- [x] Regressionen für Ogg/Vorbis und FLAC ergänzen. Beide lesen mit der
  Musikpuffergröße 8192 Byte bis EOF, starten neu und müssen identische
  PCM-Daten liefern; nach Ogg FLAC-Erweiterung bestand die gezielte Suite 3/3.
- [x] Raum-Playlistintegration mit synchronisiertem Debug-Paket nachgewiesen:
  der 20-Sekunden-Testsave-Lauf protokolliert Stream-Ende und wechselt zwischen
  zwei 0,7-s-Ogg-Tracks; `strace` bestätigt beide Dateien wiederholt.
- [x] Client-PCM-Ausgabe über PulseAudio-Monitor aufgezeichnet: 44,1-kHz-
  Stereo, 16,67 s und -24 dBFS Spitzenpegel während der Testtracks; Ausgabe
  lief in einen Null-Sink.
- [ ] Hörtest mit Vorbis-Musik im echten Client durchführen: mindestens eine
  vollständige Wiederholung und einen Wechsel zwischen zwei Playlisttracks
  abhören.
- [x] Vorbis, FLAC und Ogg FLAC aus NAP-Einträgen mit 1024-Byte-Chunks
  dekodieren, zurücksetzen und PCM vergleichen; die drei gezielten Fälle
  bestehen.
- [x] Aktiven NAP-Paketbestand im vollständigen Linux-Client ohne loses
  `DATA` starten. Ein 22-Sekunden-Debuglauf lud die Daten aus dem aktiven
  Snapshot, erreichte die Oberfläche und spielte zwei NAP-gebundene Vorbis-
  Playlisttracks abwechselnd. Die Null-Sink-Aufnahme zeigt beide generierten
  Töne auch im Frequenzspektrum. Ein zweiter Lauf bestätigte den aktiven
  Ein-Track-Loop: bei `oneshot=False` blieb der 440-Hz-Ton nach EOF im
  Null-Sink-Spektrum nachweisbar; Loop-Grenzen wurden nicht akustisch beurteilt.
- [x] NAP-gebundene Vorbis-, FLAC- und Ogg-FLAC-SFX mit `SoundData` und
  `AudioManager` auf OpenAL geprüft: alle Loop-Instanzen blieben nach 2,5 s
  aktiv; die One-Shot-Kontrollen endeten. Das war ein Engine-Audiolauf, kein
  vollständiger Spielszene-Triggernachweis.
- [x] `lancerdecode` mit MinGW-w64 als x86-64 Windows-DLL gebaut; PE32+-Format
  und native Systemimporte (`KERNEL32.dll`, `msvcrt.dll`) geprüft.
- [x] `BuildEngine --with-win64` legt die x64-PE32+-DLL bytegleich unter
  `bin/librelancer-win-x64/lib/x64/lancerdecode.dll` ab.
- [ ] Windows-Loaderauflösung und Fixture-Decodierung im CI-Runner bestätigen; der Workflow-Schritt ist eingerichtet, ein erfolgreicher Lauf steht noch aus.
- [x] Ogg FLAC separat mit synthetischem Fixture als `codec=flac` und
  `container=ogg` erkannt, dekodiert und nach EOF zurückgesetzt; auch Musik-
  und SFX-Loops sowie Endekontrollen im Audio-Backend getestet. Die gezielte
  Decoder-Suite besteht 3/3 unter Linux.
- [x] FLAC und Ogg FLAC aus einem aktiven Linux-NAP-Clientstream decodieren;
  die Deck-Playlist spielte beide synthetischen Formate wiederholt und die
  Null-Sink-Aufnahme zeigte ihre Testtöne.
- [ ] Vorbis/FLAC/Ogg FLAC unter Windows mit der gestagten DLL zur Laufzeit
  decodieren; Loaderauflösung und Windows-Audiowiedergabe bleiben offen.

**Abnahme:** Decoder- und Resettests bestehen auf jeder Zielplattform,
gestreamte Musik läuft hörbar über das Trackende hinweg, und Paketstarts laden
den passenden nativen Decoder. Ein Parser- oder Decoder-Unit-Test allein gilt
nicht als hörbarer Laufzeitnachweis.

### Phase 6 – Regression und Abschluss je Datenfamilie

- [ ] Für jeden bestätigten Fix fokussierte Parser-/Engine-Tests und den
  zugehörigen Laufzeittest ausführen.
- [ ] Client-Patches aus einer frischen LibreLancer-Basis rekonstruieren und
  `python3 scripts/verify-lancer-nexus-overlay.py` ausführen.
- [ ] Betroffene Build-Konfigurationen bauen; bei Render- und Medienpfaden
  zusätzlich den tatsächlichen Client auf der betreffenden Zielplattform
  starten.
- [ ] Logs, Testfälle, Plattformgrenzen und verbleibende Data-Aufgaben in
  `data-client-todo.md` bzw. den zugehörigen Statusdokumenten nachtragen.

**Abnahme:** Keine Aufgabe wird allein wegen eines erfolgreichen Builds oder
Parsers als visuell/funktional abgeschlossen markiert. Jede erledigte Aufgabe
hat passende Tests oder einen dokumentierten manuellen Laufzeitnachweis.

## Abhängigkeiten und Arbeitsblöcke

1. Phase 0 ist Voraussetzung für vergleichbare Laufzeitaussagen.
2. Phase 1 läuft vor gezielten Codekorrekturen und kann parallel zur
   Datentest-Vorbereitung erfolgen.
3. Phase 2 liefert die reproduzierten Befunde für Phase 2a; konkrete
   Client-Patches folgen nur aus bestätigten Engine-Lücken.
4. Phase 3 und Phase 4 sind unabhängig. Die Musik-INI-Umstellung folgt auf
   den echten Clienttest; Phase 4a auf Format-/Plattformnachweis und die
   Entscheidung, Startup-Movies wiederzugeben.
5. Phase 5 prüft Decoder- und Loop-Pfade vor NAP-Audio-Aliases oder
   Konvertierungsarbeit. Windows- und NAP-Prüfungen bleiben offen.
6. NAP- oder Updater-Änderungen sind keine Voraussetzung, solange dieselben
   lokalen Daten reproduzierbar über die bestehende Verzeichnisinstallation
   geladen werden. NAP-Laufzeit wird anschließend separat geprüft, sobald der
   relevante Loader-/Decoderpfad das Paket-Backing tatsächlich nutzt.

## Quellen und Statusführung

- Aufgabenübersicht: [data-client-todo.md](data-client-todo.md)
- Data-Quellen: [Asteroiden](../../Data/TODO-asteroiden.md),
  [Schwarze Löcher](../../Data/TODO-blackhole.md), [Musik](../../Data/TODO-musik.md),
  [Solar/Nebel](../../Data/TODO-solar.md), [Video](../../Data/TODO-video.md)
- Audio-Decoder- und Paketbefunde: [Audio-Pipeline](asset-packages/audio-pipeline.md)
- Änderungen an Reihenfolge oder Abnahmebedingungen werden in diesem Plan
  festgehalten. Einzelne TODO-Checkboxen werden erst nach belegter Prüfung
  aktualisiert.
