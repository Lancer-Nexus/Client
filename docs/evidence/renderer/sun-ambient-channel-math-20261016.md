# Sonnenambient: Kanalwirkung der vorherigen Mischung (2026-10-16)

## Quellbefund

Vor Patch 2151 bildete `RenderHelpers.ApplySunAmbient()` für passende persistente Systemlichter einen lokal nach Distanz gewichteten Farbmittelwert. Danach berechnete es:

```
sunTintedAmbient = ambient * sunColor
result = ambient * 0.75 + sunTintedAmbient * 0.25
```

Die NAP-`LightSource`-Farben werden als normalisierte RGB-Werte verwendet. Für einen Farbkanal `c` der Sonnenfarbe im Bereich `[0, 1]` gilt daher `resultChannel = ambientChannel * (0.75 + 0.25*c)`: der Ambient-Kanal bleibt gleich oder wird dunkler. Weiß ist unverändert; Rot kann Rot nicht verstärken, sondern nur Grün und Blau absenken. Eine sichtbare Farbverschiebung ist möglich, aber der Blend fügt keine sonnenfarbene diffuse Energie hinzu.

Der Befund betrifft ausschließlich die vorherige Ambient-Mischung und sagt nichts über die separate direkte Beleuchtung durch `LightSource` aus. Patch 2151 erhält die Rec.-709-Luminanz des getönten Ambient-Werts, sofern dieser im normalisierten Bereich bleibt; neue Laufzeitbilder zur Wirkung dieser Änderung stehen noch aus.

## Laufzeitprüfung

Der am 2026-10-16 frisch aus den aktuellen `Data/NAP/`-Quellen gebaute NAP-only Release-Lauf lud die Daten und erreichte die M01A-Szene. Nach dem Intro führte der hier erreichte Ablauf in die Freeport-7-Basis-/Stadtansicht. Diese Szene eignet sich nicht als Beleg für die Systemsonnenfarbe; der schwarze Zwischenframe war ein Szenenübergang, kein bestätigter Client-Crash. Es wurde kein geeignetes Raum-A/B-Paar aufgenommen und die Laufzeitformel nicht visuell kalibriert.

## Nächste Messung

- Einen ruhenden Spielstand außerhalb einer Nebel-Ambient-Überschreibung verwenden.
- Dieselbe laufende Szene, Pose, Kamera und Framezeit für Ein/Aus aufnehmen.
- Einen möglichst unbeleuchteten oder neutralen diffusem Materialbereich im roten HI02 und grünen EW06-Lichtbereich vergleichen; BR02 weiß bleibt Kontrollfall.
- Pixelwerte auf einer festen Objektregion vergleichen und authored Ambient, Sonnenfarbe, resultierende CPU-Farbe sowie Materialpfad dokumentieren.
- Erst danach Blendform und Stärke ändern; den Legacy-Pfad bei ausgeschalteter Option exakt erhalten.
