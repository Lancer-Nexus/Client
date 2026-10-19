# BW04-Sonnenambient: explorative Laufzeitbilder (2026-10-17)

Diese Aufnahmen dokumentieren einen HDR-Lauf mit Sonnenambient an und aus.
Sie sind **kein gültiger Bildvergleich** und belegen weder Farbwirkung noch
Qualität des Effekts: Die Starts zeigen unterschiedliche Explosionen und
Effektpositionen, obwohl derselbe Save-Klon mit derselben gespeicherten
Spielerposition und -ausrichtung verwendet wurde. Die Bilder bleiben hier als Diagnosebeleg für den Testaufbau und
seine Grenze erhalten.

| Sonnenambient | Aufnahme |
| --- | --- |
| Aus | [BW04 ohne Sonnenambient](bw04-ambient-off.png) |
| An | [BW04 mit Sonnenambient](bw04-ambient-on.png) |

## Laufbedingungen

- Beide Starts verwendeten den Release-Client `03bfd87-git (20261013)` auf
  OpenGL 4.6 / Mesa 23.2.1 / Intel HD Graphics 4600.
- Beide Logs bestätigen `Finished loading game data`, `World restore completed
  for Bw04` und `Entering system Bw04`.
- Die Läufe verwendeten denselben isolierten Save-Klon mit `system = Bw04`,
  `pos = 0,0,10000` und `rotate = 180,0,0`. Der Quellsave und das Benutzerprofil
  blieben unverändert.
- Startoptionen waren identisch bis auf
  `--render-sun-ambient=off|on`; beide verwendeten `--render-hdr=on` und
  `--render-bloom=off`. Damit lief der Ambient-Pfad mit aktiv angefordertem
  HDR-Tonemapping.
- Das Laufzeitverzeichnis enthielt kein loses `DATA`. Manifest
  `lancer-renderer-aa-profile-20261016` hatte SHA-256
  `e6d56a0f74095c89712f35f35586bfbe5db19d28f0f106654037a53140fc3ca1`.
  Größen und SHA-256 aller 13 Archive stimmten mit `active.json` überein;
  `nexus-pack verify` bestätigte alle 13 Archive.
- Die Screenshots entstanden kurz nach dem Eintritt in BW04, ohne Pausemenü.
  Der sichtbare Effektverkehr variiert zwischen den Prozessen. Unterschiedliche
  Explosionen dominieren den Pixelvergleich und verhindern eine Aussage über
  das Sonnenambient.

## Ergebnis und nächster Schritt

Die Daten- und Startkonfiguration für einen HDR-Lauf in BW04 ist verifiziert;
die bildliche Abnahme bleibt offen. Für einen aussagekräftigen Vergleich muss
derselbe pausierte Simulationszustand im laufenden Prozess mit Ambient an und
aus aufgenommen werden, oder die Szene muss ohne zufällige Effekte reproduziert
werden. Stärke und Farbwirkung sind damit nicht kalibriert.
