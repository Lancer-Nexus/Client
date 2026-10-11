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

- [ ] Verwendete lokale Freelancer-Dateninstallation und deren Stand für den
  Test protokollieren, ohne Dateien oder Zugangsdaten zu veröffentlichen.
- [ ] Repräsentative Testfälle pro TODO auswählen: Asteroidenfeld,
  Omega-13-Schwarzes-Loch/Ringe, Solar-/Nebelkonfiguration, Bar-Raum-Musik und
  Startup-Video.
- [ ] Fehlende Asset-Referenzen anhand der Data-Quelldaten und eines
  reproduzierbaren Inventarlaufs abgleichen. Die aktuelle `missing.md` hat 48
  Zeilen: 17 zeigen auf nicht vorhandene Intro-Missions-INIs, acht
  pfadlose Verweise sind laut Datei unter `bases/generic/` vorhanden, und 23
  verbleibende Ziele werden als fehlend geführt. `README.md` (32) und
  `WISSEN.md` (21) stimmen mit keiner dieser Zählweisen überein; die genaue
  Definition und ein reproduzierbarer Generator fehlen noch. Bis dahin keine
  fehlende Ressource automatisch dem Client zurechnen.
- [ ] Client-Start, Datenladefehler und Screenshots/Logs als reproduzierbare
  Ausgangsbasis erfassen. Testdaten bleiben lokal.
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

- [ ] Für Asteroidenfelder, Black-Hole-/Nebel-/Ringwerte sowie Solar-/Planeten-
  und Nebelwerte den Weg vom Schema über `GameItemDb` bis zum konsumierenden
  Laufzeitcode dokumentieren.
- [ ] HD-Edition-Felder (`fill_dist`, `start_dist`, Dichte/Farbe,
  `shape`, `exclusion`, `zone`, `render_parts`, `fade` und relevante
  Solarparameter) gegen tatsächliche Parser- und Renderer-Verwendung prüfen.
- [ ] Fehlende und ungültige optionale Ressourcenfälle erfassen. Parser- und
  Regressionstests nur für bestätigte Client-Grenzen ergänzen.
- [ ] Vor Änderungen an Musik- oder Videoformaten den aktuellen Loader,
  Decoder, Plattformpfad, Loop-/Synchronisationsverhalten und Fehlerpfad
  anhand des Codes belegen.

**Abnahme:** Jedes untersuchte Feld hat einen belegten Verbraucher oder ist
als ungenutzt/offen markiert. Parserlücken haben einen kleinen reproduzierbaren
Testfall.

### Phase 2 – Visuelle Datenlaufzeit: Asteroiden, Solar, Nebel und Ringe

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
- [ ] Wiedergabe und Trackwechsel in `Br0104Bar` mit einem realen lokalen
  Datenbestand prüfen; dies erfordert einen Client-Laufzeittest.
- [ ] Erst nach bestandener Client-Abnahme die Bar-INIs im Data-Repository
  ändern und dessen SDK-INIs in NAP synchronisieren.

**Abnahme:** Bestehende Einzeltrackräume verhalten sich unverändert; Playlist-
und Wechselverhalten sind durch Tests und einen Laufzeitversuch belegt.

### Phase 4 – Startup-Videos und Zielplattformen

- [ ] Den vollständigen `StartupMovies`-Aufrufpfad finden oder bestätigen,
  dass derzeit keine Wiedergabe angebunden ist.
- [ ] Decoder und unterstützte Formate für jede Zielplattform aus dem Client-
  Code und einem echten Laufzeittest bestimmen.
- [ ] Mit einem synthetischen oder anderweitig rechtmäßig bereitgestellten
  Testclip Bild, Ton, Synchronität, Abbruch, Fehler und fehlenden Decoder
  prüfen. Keine Originalvideos als Testfixture committen.
- [ ] MP4/H.264/AAC nur nach erfolgreichem Test auf den Zielplattformen als
  Clientformat freigeben. WMV bleibt bis dahin im Data-Bestand.
- [ ] Video-Upscaling/-Konvertierung, NAP-Ablage und Schutz vor Überschreiben
  bleiben getrennte Data-Arbeiten.

**Abnahme:** Der Pfad von der Konfiguration bis zur Wiedergabe ist belegt;
Formatfreigaben enthalten Plattform- und Testnachweis.

### Phase 5 – Regression und Abschluss je Datenfamilie

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
3. Phase 2 ist ein gemeinsamer visueller Testblock; konkrete Client-Patches
   folgen nur aus reproduzierten Engine-Lücken.
4. Phase 3 und Phase 4 sind voneinander unabhängig, brauchen aber jeweils
   einen belegten Wiedergabepfad und Plattformtests.
5. NAP- oder Updater-Änderungen sind keine Voraussetzung, solange dieselben
   lokalen Daten reproduzierbar über die bestehende Verzeichnisinstallation
   geladen werden. NAP-Laufzeit wird anschließend separat geprüft, sobald der
   relevante Loader-/Decoderpfad das Paket-Backing tatsächlich nutzt.

## Quellen und Statusführung

- Aufgabenübersicht: [data-client-todo.md](data-client-todo.md)
- Data-Quellen: [Asteroiden](../../Data/TODO-asteroiden.md),
  [Schwarze Löcher](../../Data/TODO-blackhole.md), [Musik](../../Data/TODO-musik.md),
  [Solar/Nebel](../../Data/TODO-solar.md), [Video](../../Data/TODO-video.md)
- Änderungen an Reihenfolge oder Abnahmebedingungen werden in diesem Plan
  festgehalten. Einzelne TODO-Checkboxen werden erst nach belegter Prüfung
  aktualisiert.
