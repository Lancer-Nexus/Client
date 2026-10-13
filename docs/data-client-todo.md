# Freelancer Data: offene Client-Arbeiten

Diese Liste überträgt aus dem benachbarten [Data-Repository](../../LN-Data/README.md)
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

Quelle: [LN-Data/TODO-musik.md](../../LN-Data/TODO-musik.md)

Stand: Patch `2171-room-music-playlists.patch` (Commit `7a3518ce8`) setzt die Parser- und Auswahllogik um.

- [x] `[Room_Sound] music` nimmt mehrere Nicknames und `oneshot` an (`RoomSound.MusicTracks`, `BaseRoom.MusicTracks`); ein einzelner Nickname und `oneshot` verhalten sich wie zuvor.
- [x] Wechselverhalten: zufällige Auswahl ohne direkte Wiederholung (`RoomMusicPlaylist`), nächster Titel, sobald der aktuelle endet; bei mehreren Titeln spielt jeder einmal, ein einzelner Titel schleift weiter.
- [x] Parser- und Auswahltests (`RoomMusicPlaylistTests`: mehrere Titel mit `oneshot`, einzelner Titel mit `oneshot`, keine Wiederholung, null/ein Titel).
- [ ] Wiedergabetest im Client: Raum mit mehreren Titeln (`Br0104Bar.ini` o. ä.), Wechsel am Titelende, Verhalten mit `oneshot` (bei `oneshot` wird einmalig ein Titel gespielt und nicht gewechselt), Raumwechsel.
- [ ] Audioformat beachten: Die Musik liegt in `LN-Data` seit der Konvertierung als Ogg Vorbis (`.ogg`) vor. Den Streaming-Pfad (`StreamingSource`) mit `.ogg` prüfen, besonders das Ende eines Titels (`Game.Sound.MusicPlaying`) und lückenlosen Wechsel.
- [ ] Erst nach erfolgreichem Engine-Test die Bar-Raum-INIs im Data-Repository auf Playlists umstellen und anschließend dessen INIs nach NAP synchronisieren (Datenaufgabe, Räume und Titel in `TODO-musik.md`).

### Asteroidenfelder und Asteroidentexturen

Quelle: [Data/TODO-asteroiden.md](../../LN-Data/TODO-asteroiden.md)

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

Quelle: [Data/TODO-blackhole.md](../../LN-Data/TODO-blackhole.md)

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

Quelle: [Data/TODO-solar.md](../../LN-Data/TODO-solar.md)

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

Quelle: [Data/TODO-video.md](../../LN-Data/TODO-video.md)

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

- [Asteroiden](../../LN-Data/TODO-asteroiden.md)
- [Schwarze Löcher](../../LN-Data/TODO-blackhole.md)
- [Musik](../../LN-Data/TODO-musik.md)
- [Solar und Nebel](../../LN-Data/TODO-solar.md)
- [Video](../../LN-Data/TODO-video.md)

## Ergänzungen aus LN-Data (Stand 2026-10-11)

Datenstand in `LN-Data`: SDK 1.3.1 mit den INIs des HD-Packs (HD-Werte gelten), 7369 Nicht-INI-Dateien im NAP (elf Klassenordner), 343 per KI um Faktor 4 hochskalierte Texturcontainer (605 Texturen), 1700 Audiodateien von WAV nach Ogg Vorbis (1620) und FLAC (80) gewandelt, INIs auf `.ogg`/`.flac` umgestellt. 19 712 Datei-Verweise geprüft, 32 Ziele fehlen (`LN-Data/missing.md`, erzeugt von `tools/check_refs.py`; die Zahlen in README, WISSEN und missing.md stimmen jetzt überein). Struktur geprüft, in keiner Engine gestartet. Die Plugin-Quellen liegen in `LN-Data/reference/freelancer-plugins/`, die Analyse in `LN-Data/TODO-plugins.md`.

### Daten laden (NAP, HD-Pack, hochskalierte Texturen, Audio)

- [ ] `NAP/` mit `nexus-pack pack` zu NAP-V1-Paketen bauen (Vorschlag: Basis, HD-Pack, Hochskalierung, INIs getrennt) und mounten. Override-Deklarationen aus `tools/hd_overrides.txt` (1603) und `tools/upscaled_containers.txt` (343) ableiten.
- [ ] Client und LLServer mit diesen Daten bis zum ersten UI und in ein System starten (Linux). Das ist der fehlende Spieltest.
- [ ] 1024- und 2048-Pixel-Texturen (DXT1/DXT5, TGA) laden, besonders Planeten (256 → 2048) und `solar/rings/rings.txm`.
- [ ] Hochskalierte Container laden: DXT1/3/5, DDS unkomprimiert 24 Bit, TGA 8/16/24/32 mit Mip-Kette (neu geschriebener UTF-Container, `tools/utf_write.py`). Bei Fehlern den Container aus der Sicherung zurückholen und aus `upscaled_containers.txt` nehmen.
- [ ] Die 10 neuen HD-INIs lesen; die vier Plugin-INIs (`Storyfactions.ini`, `Territory.ini`, `Ranks.ini`, `HudShift.ini`) müssen ohne Plugin wirkungslos bleiben.
- [ ] **Audio:** Die 1700 Audiodateien sind `.ogg` (Vorbis, 1620) und `.flac` (80); die INIs verweisen darauf (`audio/Sounds.ini`, `Music.ini`, `StorySounds.ini` u. a.). Prüfen, dass Client und LLServer sie über `GameDataManager` und den NAP-Stream dekodieren, auch Streaming-Musik, dass Dialoge und Effekte ohne Alias laden und dass die Schleifen lückenlos laufen. Es gibt keine Loop-Start/Ende-Metadaten (`audio-pipeline.md`). Opus ist ausgeschlossen. Ein `.thn` (`s059x_nrml_jonner_rh_01_bar_01.thn`) verweist auf `.ogg`.
- [ ] Verhalten bei den 32 fehlenden Zielen: Kollisionsmodelle `*_col.cmp` (5), 17 `spacepop`-INIs, `tlr_a/d/tube_a`, `trading_depot`, `roids.txm`, `hudtarget.txm`, `exit/openingscreen.tga`. Absturz, Warnung oder stiller Fallback?

### Plugin-Funktionen des HD-Packs

Das HD-Pack lädt über `EXE/dacom.ini` rund 20 Client-Plugins für `Freelancer.exe` 1.1. Sie sind **Binärpatches im Prozessspeicher**: Sie überschreiben beim Laden feste Adressen in `Freelancer.exe` oder `common.dll`, hängen eigene Hooks ein und lesen ihre Einstellungen mit dem `INI_Reader` aus `common.dll`. Adressen und Patches sind deshalb nicht übertragbar. Gebraucht wird die **Funktion**, nicht der Code; sie soll in LibreLancer als normales Feature mit der vorhandenen INI umgesetzt werden (Format unverändert).

**Referenzen** (alle im Repository `LN-Data`, Ordner `reference/freelancer-plugins/`; dort `README.md` mit Herkunft, Stand und Lizenz je Plugin; Analyse und Spezifikation in `TODO-plugins.md`):

- `adoxa/<plugin>/`: Quellcode von Jason Hood, Quelle `https://adoxa.altervista.org/freelancer/plugins.html`. Lizenz laut Seite: „freely used by any mod“, keine formale Lizenzdatei.
- `github/<plugin>/`: Quellcode aus eigenen GitHub-Repos mit jeweils eigener Lizenz (Tabelle „Quellen im Netz“).
- Die kompilierten DLLs sind nicht enthalten; nichts davon wird gebaut oder geladen.

#### Priorität A: ohne diese Funktionen sind die HD-INIs wirkungslos

| Plugin (Version) | Funktion | Konfiguration | Wirkung ohne Plugin | Quellcode (LN-Data) |
|---|---|---|---|---|
| **Story Factions** 1.01 | Legt fest, welche Fraktionen in der Reputationsliste als Story-Fraktionen ausgeblendet werden. Die Liste ist im Original fest einprogrammiert, die INI ersetzt sie vollständig. | `DATA\Storyfactions.ini`, `[StoryFactions]`, ein Fraktions-Nickname pro Zeile (`fc_ln_grp`, `fc_kn_grp`, `fc_rn_grp`, `fc_n_grp`, `fc_or_grp`, `fc_f_grp`, `fc_q_grp`, `fc_ouk_grp`, `fc_uk_grp`). Fehlt die Datei: eingebaute Liste. | keine | `adoxa/storyfactions/` |
| **Territory** 1.03 | Ändert die Meldung beim Systemeintritt („X System, Y Space.“) und ergänzt Häuser auf der Nav-Map. Im Original sind Territorien und die vier Häuser (Li, Br, Ku, Rh) fest eingebaut. | `DATA\UNIVERSE\Territory.ini`: `[Settings]` (`caps`, `codepage`), `[Houses]` (`format`/`format_id` mit `%s` für System und Haus, `house`/`house_id`, `systems` mit Nickname oder Bereich wie `Li01-5`, `NavMap = X, Y[, Schrift]`), `[Systems]` (`format`, `systems`) | keine | `adoxa/territory/` |
| **Ranks** 1.00 | Zeigt zum Level einen Rangnamen („Pilot 12“); im Mehrspielermodus je Fraktion. Rangnamen sind Stringressourcen in der DLL. | `DATA\MISSIONS\ranks.ini`: `[SP Ranks]`, `[MP Ranks]` (je Fraktion `group = <nickname>`), `[Rank Settings]` (`just_levels`, `offset`); Schlüssel `next_rank = LEVEL[, ID]`, `set_ranks = INTERVAL, COUNT[, ID]`, `format` (`%%d` Level, `%s` Rang), `first_rank_id` | keine | `adoxa/ranks/` |
| **HUD Shift** 2.12.1 | Verschiebt HUD-Elemente, damit sie bei Breitbild am Rand bleiben; Werte je Seitenverhältnis (16:9, 15:9, 16:10, 5:4). | `DATA\INTERFACE\HudShift.ini`: `[HUDShift]` (`Horizontal = auto` oder Wert, `Vertical`, `GaugeBars`, `PlayerStatus`, `TargetStatus`), `[Group]` (`nickname`, `location = left/right/top/bottom`, `position`). Die Adressen in `position` sind absolute EXE-Adressen und müssen durch Anker ersetzt werden. | HUD bei Breitbild falsch platziert | `adoxa/hudshift/` |

Umsetzungshinweise: Story Factions ist eine konfigurierbare Ausschlussliste für das Reputationsfenster. Territory braucht datengetriebene Territorien, Nav-Map-Beschriftung aus der INI und Formatstrings mit Platzhaltern (`^` = Zeilenumbruch, höchstens 63 Zeichen). Ranks braucht eine Rang-Tabelle je Fraktion mit lokalisierten Namen. HUD Shift soll die Verschiebung aus dem Seitenverhältnis berechnen und mit Ankern statt Adressen arbeiten.

#### Priorität B und C: Mehrspieler, Bedienung, Komfort

| Plugin (Version) | Funktion | Priorität | Hinweis | Quellcode (LN-Data) |
|---|---|---|---|---|
| **MP Rep** 1.03 | Holt im Mehrspielermodus die Reputation vom Server für politische Zonen, Patrouillen und Schiffskauf (ersetzt `GetGroupFeelingsTowards`; benötigt auch einen Server-Patch). | B | Protokollpunkt mit dem Gateway (Reputation, Besitz, Lease). Original wertet clientseitig aus. | `adoxa/mprep/` |
| **Zone** 1.20 | Meldet Namen benannter Zonen beim Betreten und Verlassen, Farbe nach Gefahr (`property_flags`, Bits 3–5). | B | Parameter `upper`, `low=`, `medium=`, `high=`, `ultra=`; Texte als Ressourcen. | `adoxa/zone/` |
| **Wheel Scroll** 1.12 | Mausrad in Fenstern (Reputation, Kontakte, Waffen, Chat). | B | kleiner Aufwand | `adoxa/wheelscroll/` |
| **Turret Zoom** 1.41 | Mausrad steuert die Kamera der Turmansicht, Modifikatoren Shift/Ctrl/Alt. | B | Parameter im HD-Pack: `inverse max=1000 shift=200 ctrl=20 alt=4 s+c=0.4 c+a=0.2 switch=100 interval=500 adjust=0.75` | `adoxa/turretzoom/` |
| **flplusplus** | Konfigurierbare Sichtweite/LOD, TTF-Schriften, Speicher- und Screenshot-Pfad, FPS-Limit, Fehlerkorrekturen. | B | HD-Daten brauchen größere Sichtweite und eigene Schriften. | `github/flplusplus/` |
| **FLSharp** | Hook-basierte Korrekturen, Auflösungsliste, Hauptmonitor als Standard. | B | Die Fehlerliste (siehe unten) als Abnahmetests übernehmen. | `github/FLSharp/` |
| **HUDless** 1.02 | HUD per Taste ausblenden (`MINIMIZE HUD`, `OPEN LOG`); HUD, dann Cursor, dann beides wiederherstellen. | C | | `adoxa/hudless/` |
| **Hail** 1.01 | Taste für „Hail“ (Eintrag `USER_RADIO` in `keylist.ini`). | C | | `adoxa/hail/` |
| **Numeric Damage** 1.00 | Zeigt Trefferpunkte (Hülle, Schild) numerisch. | C | | `adoxa/numeric/` |
| **MultiIntro** | Mehr als drei Intro-Bildschirme, zufällig. | C | **GPL-3.0**: kein Code übernehmen ohne Lizenzprüfung. | `github/MultiIntro-Updated/` |
| **DelayDisplay** | Waffen-Abklingzeit in der Kontaktliste. | C | **Keine Lizenz** (Dekompilation des Plugins von M0tah). Variante mit Lizenz: `TheStarport/FLUF`, `modules/delay_display` (AGPL-3.0, nicht kopiert). | `github/DelayDisplay/` |
| **TargetScreen** | Dynamische Zielansicht (Nachbau von Venemons Dynamic Target Screen). | C | MIT | `github/DynamicTargetScreen/` |

**FLSharp-Fehler, die der neue Code nicht wiederholen darf:** Handelsrouten-Lichter bleiben aus; Wegpunkte werden im falschen System gelöscht; Mehrfachlauf-Waffen (Mündungsfeuer, Ton, Munitionszähler); `use_animation` aus `weapon_equip.ini`; Client-Server-Updates (mindestens alle 2 s, 40 ms Minimum, 750 ms in der Handelsroute); Rotationssperre beim Start; Kopieren/Einfügen in Eingabefeldern; Abstürze bei fehlerhaften Spielständen; Infokarten für dynamische Solare.

#### Entfällt oder gehört woanders hin

| Plugin | Grund | Quellcode (LN-Data) |
|---|---|---|
| MP3 Codec Fix 1.12 | findet den Fraunhofer-MP3-Codec; im neuen Audiosystem kein Thema | `adoxa/mp3codec/` |
| Console 1.24 | Entwicklerkonsole für den Einzelspielermodus (mehrere Schiffe besitzen u. a.), im Mehrspielermodus nicht vorgesehen | `adoxa/console/` |
| JFLP.DLL 1.27 | Sammelpatch für Steuerung und Anzeige; Funktionen einzeln bewerten | `adoxa/jflp/` |
| Lighting Bug Fix | Beleuchtungsfehler; im HD-Pack deaktiviert, hängt am alten Renderer (MIT) | `github/fl-lighting-bug-fix/` |
| cyrillic_chat | kyrillischer Chat; im HD-Pack deaktiviert (MIT) | `github/cyrillic_chat/` |
| VersionText | Quellcode nicht auffindbar (verlinktes Repo liefert 404) | – |
| FLHook | Serverseitiges Hook-Framework (TheStarport, GPL-3.0), gehört zur Serverarchitektur und wird über `FLHookCompat` getrennt bewertet | `github/FLHook/` |

#### Quellen im Netz

| Plugin | Quelle |
|---|---|
| adoxa-Plugins (Jason Hood) | `https://adoxa.altervista.org/freelancer/plugins.html` |
| flplusplus | `https://github.com/FLHDE/flplusplus` (BSD-3-Clause) |
| FLSharp | `https://github.com/BC46/FLSharp` (MIT) |
| MultiIntro | `https://github.com/FLHDE/MultiIntro-Updated` (GPL-3.0) |
| DelayDisplay | `https://github.com/FLHDE/DelayDisplay` (ohne Lizenz) |
| TargetScreen | `https://github.com/FLHDE/DynamicTargetScreen` (MIT) |
| cyrillic_chat | `https://github.com/FLHDE/cyrillic_chat` (MIT) |
| Lighting Bug Fix | `https://github.com/FLHDE/fl-lighting-bug-fix` (MIT) |
| FLHook | `https://github.com/TheStarport/FLHook` (GPL-3.0) |

#### Vorgehen

1. Priorität A mit den vier vorhandenen INIs umsetzen, Formate unverändert.
2. MP Rep als Protokollpunkt mit dem Gateway abstimmen.
3. FLSharp-Fehlerliste als Abnahmetests übernehmen.
4. Komfort (Zone, Wheel Scroll, Turret Zoom, flplusplus-Optionen), dann Priorität C nach Bedarf.
5. Prüfen, ob `Freelancer.ini`/`dacom.ini` in LibreLancer überhaupt Plugin-Einträge braucht oder die Funktionen in der Engine bleiben.
6. Erwartung testen: Die vier INIs bleiben ohne Plugin wirkungslos.

**Lizenz:** GPL-3.0-Code (MultiIntro, FLHook) nicht ohne Prüfung übernehmen. Für MIT/BSD gilt Namensnennung. DelayDisplay hat keine Lizenz. Die adoxa-Plugins nennen nur „freely used by any mod“.

### Weiterer Spielinhalt

- [ ] **HD-Spiellogik:** Das HD-Pack entfernt viele `faction_weight`-Einträge und ändert Skripte, Gerüchte und Encounter. Entscheidung: HD-Werte gelten. Im Spiel prüfen.

