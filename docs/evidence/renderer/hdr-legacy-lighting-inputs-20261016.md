# Lineare HDR-Eingänge für Legacy-Beleuchtung (2026-10-16)

## Umsetzung und Abdeckung

- `LinearizeLegacyInput` verwendet den aktiven Render-Farbraumblock und ist
  außerhalb des HDR-Ziels eine Identität. Die gemeinsamen Funktionen in
  `Lighting.hlsl` dekodieren Materialdiffuse, Vertexfarbe, Diffusetextur,
  Materialambient, Lichtdiffuse/-ambient, Systemambient und Fogfarbe vor der
  Beleuchtungs-/Fogberechnung.
- `CalculateVertexLighting` linearisiert Lichtfarben vor der Vertexsumme und
  lässt Summen im HDR-Pfad über 1.0 stehen. Der vorherige Clamp bleibt im LDR-Pfad.
- Basic behandelt Diffuse- und Vertexfarben getrennt; Emission/Effekttextur,
  Bt-Detailtextur und Envmap werden vor ihrer HDR-Verrechnung linearisiert.
  Die DetailMap-, Masked- und Illuminated-Detail-Varianten dekodieren die
  beteiligten Detail-/Illum-Texturen. Atmosphere- und AsteroidBand-Pfade geben
  ihre beleuchteten Ergebnisse als bereits linear aus.
- `ToLinearSceneOutput` verhindert die erneute sRGB-EOTF auf diesen Pfaden.
  Die Texturzuordnung bleibt unverändert: OpenGL verwendet BGRA8/UNORM- und
  BC-Formate ohne hardwareseitiges sRGB-Decoding.

## PBR-Farbvertrag (2026-10-17)

Der PBR-Fragmentshader linearisiert jetzt auch die direkten Lichtfarben,
Punktlicht-Ambientanteile und Systemambientfarbe vor ihrer Beleuchtungsrechnung.
Emissivfarbe und Emissivtextur werden getrennt linearisiert und erst danach
multipliziert. PBR berechnet diffuse und Ambient-Lichtbeiträge im bestehenden
Pixellicht-Loop auch dann pro Pixel, wenn die Legacy-Vertexbeleuchtung aktiviert
ist. Dadurch verwendet PBR nicht die voraggregierten, im LDR-Pfad begrenzten
Legacy-Vertexlichtsummen. Die PBR-LDR-Ausgabe nutzt jetzt die stückweise exakte
sRGB-OETF statt einer Gamma-2,2-Näherung; im HDR-Ziel bleibt die Ausgabe linear.

Der .NET-10-Releasebuild von `LibreLancer.csproj` lief nach dieser Änderung mit
0 Fehlern und 131 Warnungen; die Engine-Shadervarianten wurden dabei
kompiliert. Ein kontrollierter Bildvergleich der PBR-Materialien und ihrer
Lichtfarben steht noch aus. Nach der Änderung der PBR-LDR-Ausgabekurve lief der
Engine-Releasebuild erneut mit 0 Fehlern und 131 Warnungen; die Shadervarianten
wurden kompiliert.

## Grenzen und Prüfung

- .NET-10-Releasebuild von `src/LibreLancer/LibreLancer.csproj` erfolgreich:
  0 Fehler, 131 Warnungen. Shadercompiler-Varianten für Basic, Atmosphere und
  die gemeinsam beleuchteten Detailmaterialien wurden verarbeitet.
- Default-LDR-Rechenpfad bleibt durch die bedingte Identitätskonvertierung
  erhalten; eine Laufzeit-/Bildabnahme erfolgte nicht.
- Die Annahme, dass Material-, Licht-, Systemambient- und Fogfarben in den
  Eingabedaten sRGB-encodiert sind, ist noch nicht anhand einer festen
  NAP-Szene kalibriert. `Data/NAP/` enthält aktuell nur `.gitkeep`-Platzhalter.
  Die Wirkung auf rote/grüne Lichter, Nebel, Emission und Legacy-Materialien
  muss mit NAP-only Vergleichsbildern geprüft werden.
- Nicht beleuchtete Additiv-/Sprite-/Nebula-Effektshader folgen weiterhin ihrem
  bisherigen Ausgangsfarbpfad. Diese Aufgabe vervollständigt daher nicht den
  gesamten Renderer oder alle Shader-Farbräume.
