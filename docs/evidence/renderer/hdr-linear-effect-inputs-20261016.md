# Lineare HDR-Eingänge für zusammengesetzte Effektshader (2026-10-16)

Im opt-in HDR-Pfad linearisiert `LinearizeLegacyInput` nun die RGB-Eingänge vor
Farboperationen in den zuvor verbliebenen Legacy-Fragmentshadern:

- `Sprite`, `NebulaMaterial` und `NebulaInterior`: Textur und Tint vor Multiplikation
- `NebulaExtPuff`: Textur, Innen- und Außenfarbe vor Multiplikation/Interpolation
- `Nomad`: beide Texturen vor additiver Verrechnung
- `SunSpine` und `SunRadial`: Textur sowie Innen-/Außenfarben vor Interpolation
- `ZoneVolume`: diffuse Farbe vor dem Lichtanteil
- `Color` und `Navmap`: Eingangsfarbe beziehungsweise Textur und Tint

Alpha-Kanäle werden nicht linearisiert. Im LDR-Pfad ist die Eingangs-Konvertierung
eine Identität; bestehendes Alpha- und Blend-Verhalten bleibt erhalten. Die
bereits ausgeführten Shader-Builds prüfen die Kompilierung, nicht die visuelle
Farbkalibrierung. Eine NAP-only Laufzeitaufnahme wurde für diese Änderung nicht
ausgeführt. Die Annahme, dass diese Legacy-Farbwerte sRGB-kodiert sind, bleibt
visuell zu bestätigen.

## Überlagerungs- und Buildnachweis

- `python3 scripts/verify-lancer-nexus-overlay.py`: 749 Dateien stimmen mit
  einer sauberen Rekonstruktion der registrierten Patch-Reihe überein.
- .NET-10-Release-Build von `LibreLancer.csproj`: 21 Projekte, 0 Fehler und
  131 Warnungen. Der Build kompilierte die geänderten Shader; er ersetzt keine
  visuelle Laufzeitabnahme.
