# HDR/Bloom-Laufzeitprüfung (2026-10-16/17)

Die Prüfung startete den Client mit NAP-Archiven, die vollständig aus
`Data/NAP/.runtime-test-stage/package-inputs-20261016` gebaut wurden. Der
aktive Manifest-Hash und alle 8.684 Archiveinträge wurden vor dem Start geprüft.
Die Laufzeit lief auf OpenGL 4.6 / Mesa Intel HD Graphics 4600. Die Intro-Szene
wurde in beiden HDR-Aufnahmen erreicht.

`hdr-disabled-reference.png` zeigt HDR und Bloom ausgeschaltet.
`hdr-fixed-tonemap.png` zeigt HDR mit Bloom ausgeschaltet.
`hdr-fixed-bloom.png` zeigt HDR und Bloom bei einer Stärke von 0,5.
`hdr-default-bloom-strength.png` zeigt Bloom mit dem Standardwert 0,18. In
beiden Aufnahmen wird die Szene gerendert und um die Sonne ist ein weicher
Lichtsaum sichtbar. Die Aufnahmen entstanden in getrennten Prozessstarts und
zu leicht verschiedenen Intro-Zeitpunkten; sie belegen keine quantitative
Stärkenkalibrierung.

Der HDR-Eingang vor dem Tonemapping enthielt zuvor bereits die korrekten
Szenedaten, während die Ausgabe nahezu einfarbig blieb. Ursache war die
Vertex-Attributbelegung der Fullscreen-Pässe: `VertexPositionTexture` legt UV
auf Slot 3, die drei Shader für Tonemap, Bloom Extract und Bloom Blur hatten den
TexCoord-Eingang aber implizit auf Slot 1. Dadurch wurde der Quell-UV-Eingang
nicht belegt und die Pässe lasen eine konstante Texturkoordinate.

Patch 2177 setzt für Position und TexCoord die verwendeten Slots explizit auf
0 und 3. Nach Löschen und Neuerzeugen der drei kompilierten Shader-Bundles
zeigte der NAP-Lauf die erwartete Szene. Das ergebnislose frühere Experiment
mit expliziten Attributen hatte alte Shader-Bundles verwendet und war daher kein
gültiger Gegenbeleg.

HDR und Bloom bleiben opt-in. Der NAP-Lauf belegt Tonemapping und Bloom auf dem
genannten OpenGL-/Mesa-System. Bloom ist funktional sichtbar, seine Stärke ist
noch nicht pixelgenau kalibriert. Andere Grafik-Backends und GPUs sind nicht
abgenommen.
