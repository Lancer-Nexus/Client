# Freelancer Data: offene Client-Arbeiten

Diese Liste überträgt aus dem benachbarten [Data-Repository](../../Data/README.md)
die Aufgaben, die LibreLancer-Client, Engine, Renderer oder Laufzeit betreffen.
Die Quelldokumente bleiben maßgeblich für Änderungen an SDK-INIs, NAP-Dateien
und dem Datenimport. Originaldateien werden nicht in diesem Client-Repository
kopiert oder committet.

Stand der Sichtung: 2026-10-11. Die Quell-TODOs enthalten teils veraltete
Statuszeilen wie „Änderungen sind noch nicht committet“; diese wurden nicht als
Client-Aufgaben übernommen.

## Client-Aufgaben

### Musik-Playlists in Räumen

Quelle: [Data/TODO-musik.md](../../Data/TODO-musik.md)

- [ ] Den Ablauf von `[Room_Sound] music` vom INI-Parser bis zur Wiedergabe
  dokumentieren. Der aktuelle Code speichert ein einzelnes `Music`-Feld und
  optional `MusicOneShot`; `SoundManager.PlayMusic` löst einen einzelnen
  Nickname auf.
- [ ] Eine kompatible Syntax für mehrere Nicknames festlegen und im Parser
  unterstützen; bestehende Einzelwerte und `oneshot` müssen unverändert
  funktionieren.
- [ ] Wechselverhalten festlegen: Auswahlmodus, Vermeidung direkter Wiederholung,
  Wechsel bei Raumwechsel oder Timer sowie Verhalten mehrerer Titel mit
  `oneshot`.
- [ ] Parser- und Wiedergabetests ergänzen, einschließlich eines Raums mit
  mehreren Titeln und eines einzelnen Titels mit `oneshot`.
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

- [ ] Den Client-Aufrufpfad für `Freelancer.ini`-`StartupMovies` vom Parsen bis
  zur Wiedergabe nachvollziehen. Bei der Sichtung wurde die INI-Liste gefunden;
  der dazugehörige Wiedergabeaufruf ist noch nicht belegt.
- [ ] Unterstützte Video-/Audioformate und Decoder auf den Zielplattformen
  feststellen. MP4/H.264/AAC erst dann als Clientformat freigeben, wenn ein
  echter Wiedergabetest im Client gelingt.
- [ ] Für einen synthetischen oder anderweitig rechtmäßig bereitgestellten
  Testclip Bild, Ton, Synchronität, Abbruch und Fehlerverhalten prüfen; keine
  Originalvideos als Testfixture in Git ablegen.
- [ ] WMV als Spielasset beibehalten, bis Client- und Zielsystemtests ein
  alternatives Format bestätigen. Upscaling, Konvertierung, NAP-Ablage und
  `build_nap.py`-Schutz vor Überschreiben bleiben Data-Aufgaben.

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
