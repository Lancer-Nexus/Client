# Sonnenambient: BW04 Ein/Aus-Abgleich (2026-10-16)

> Historische Messung mit der vorherigen Produktmischung vor Patch 2151. Sie ist
> keine Bildabnahme für die inzwischen luminanzerhaltende Farbmischung.

## Lauf und Daten

- Zwei getrennte Release-Starts mit demselben statischen Testspielstand,
  derselben NAP-Laufzeit, derselben Auflösung und demselben System. Nur
  `--render-sun-ambient=on` beziehungsweise `off` änderte den Schalter.
- Beide Läufe erreichten `World restore completed for Bw04` und
  `Entering system Bw04`. Der lokale Server startete mit
  `--invulnerable=on`, damit der Testspielstand nicht durch NPC-Schaden endet.
- Der BW04-Systemdatensatz hat `[Ambient] color = 60, 40, 20` und ein
  persistentes gerichtetes Licht mit `color = 255, 150, 40`. Die System-INI
  enthält `starsphere_bw04.cmp` als Hintergrund-Nebelbild; dieses ist kein
  lokaler Nebel-Ambient-Override.
- Aktiver NAP-Manifeststand: `lancer-engine-fire-qa-20261016-r2`, 13 Pakete.
  Vor den Starts wurden Größe und SHA-256 aller Archive gegen
  `active.json` geprüft. Manifest-SHA-256:
  `b321ed9b93e97350336028bb1d9555c7d435b8751e9fd8928a4ca9ef7b2744e0`.
  Das Laufzeitverzeichnis enthielt kein loses `DATA`-Verzeichnis.
- Renderer: `03bfd87-git (20261013)`, OpenGL 4.6/Mesa 23.2.1 auf Intel HD
  Graphics 4600, Desktopaufnahme 1920×1080. Das getestete
  `LibreLancer.dll` entspricht dem aktuellen Release-Build
  (SHA-256 `71f883c2996a93bc86ebd8c5a3e21ee3c77191358dc60890fbd07b4280e940fb`).
- Die Aufnahme entstand zwei Sekunden nach dem Systemeintritt. Beide Starts
  verwendeten bis auf den Ambient-Schalter denselben Befehl und denselben
  Spielstand:

  ```
  dotnet lancer.dll client.ini --render-sun-ambient=on|off \
      --invulnerable=on --load-save bw04-static.fl
  ```

| Ambient | Aufnahme |
| --- | --- |
| Ein | [BW04 mit Sonnenambient](bw04-ambient-on.png) |
| Aus | [BW04 ohne Sonnenambient](bw04-ambient-off.png) |

Bild-SHA-256: Ein `c1ad8d97bb7d5147ca39d5d7d77fa7bdb7afcb41c33764f0db37ec62f61d95a0`,
Aus `42955855cac55a07f1c4024fb4d422b58ef0b43251c71537df66fc17336db864`.

## Messung und Aussagegrenze

Der statische Bildbereich außerhalb des Schiffs ist pixelgleich. Im festen
Schiffsausschnitt `(860, 820)–(1090, 945)` betrugen die mittleren RGB-Werte:

| Zustand | R | G | B |
| --- | ---: | ---: | ---: |
| Ein | 68,847 | 35,830 | 11,585 |
| Aus | 70,265 | 37,570 | 12,537 |

Die Differenz ist klein (mittlere absolute Abweichung 1,37 RGB-Stufen im
Ausschnitt). Bei eingeschaltetem Pfad wurde kein Farbkanal sichtbar heller;
der Gesamtmittelwert sank leicht. Das ist mit der geprüften Formel vereinbar,
die authored Ambientwerte mit normalisierten Lichtfarben multipliziert und
dadurch nur gleichbleibt oder Kanäle abdunkelt. Eine klar wahrnehmbare rote
Aufhellung ist damit nicht nachgewiesen.

Die Starts waren getrennte Prozesse und kein Live-Toggle desselben Frames.
Der Hintergrund blieb exakt gleich, die Aufnahme schließt geringfügige
Unterschiede am Schiffsbild durch Spawn-/Kameraframe nicht aus. Der Abgleich
belegt daher die geringe Wirkung in dieser Szene, nicht die visuelle
Kalibrierung. Der mathematische Kanalbefund steht im
[Kanalwirkungs-Audit](../sun-ambient-channel-math-20261016.md). Grün- und
Weißkontrollen sowie ein Live-A/B in demselben Prozess bleiben offen.
