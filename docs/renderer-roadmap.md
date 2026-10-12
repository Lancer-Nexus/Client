# Renderer-Roadmap

Diese Liste plant moderne Renderer-Erweiterungen für LibreLancer. Sie ist eine Arbeitsliste, keine Zusage, dass die genannten Effekte bereits implementiert oder upstream abgestimmt sind. Bestehende Freelancer-Darstellung und niedrigere Hardwareprofile bleiben Abnahmekriterien.

## Bereits vorhanden

- PBR-Materialpfad in `BasicMaterial` mit Normal-, Roughness- und Metallic-Maps.
- Tangent-Space-Normalmapping im regulären FVF-Shaderpfad, sofern das Mesh die passenden Tangentdaten enthält.
- SMAA als Antialiasing-Option.
- Bewegungsunschärfe für bestimmte Partikeltypen. Das ist keine kamerabasierte Motion Blur für die ganze Szene.

„Bumpmapping“ wird hier als Normalmapping verstanden. Die Engine hat dafür bereits Teilunterstützung; die Aufgabe ist zuerst, Datenpfade, Mesh-Tangenten, Materialoptionen und Fallbacks auf vollständige Abdeckung zu prüfen, statt einen zweiten konkurrierenden Effekt einzubauen.

## Vorgeschlagene Arbeitspakete

### 1. Render-Pipeline und Profiling

- [ ] Render-Passes mit klaren Ein-/Ausgaben und expliziter Farb-/Tiefenpufferverwaltung vorbereiten.
- [ ] Render-Target-Resize, minimierte Fenster, Scissor-/Viewport-Wechsel und Geräteverlust für zusätzliche Pässe absichern.
- [ ] GPU-Zeit und Speicherverbrauch pro Pass messbar machen; Effekte müssen einzeln abschaltbar und nach Hardwareprofil skalierbar sein.
- [ ] Referenzszenen und feste Screenshots für Raum, Basis, Sternensystem, Nebel, Partikel und LancerEdit-Vorschau festlegen.

### 2. Bumpmapping / Normalmaps vervollständigen

- [ ] Alle Materialpfade für normale Freelancer-Materialien und PBR-Materialien inventarisieren und gegeneinander testen.
- [ ] Tangenten-/Bitangenten-Herkunft je Meshformat prüfen; fehlende oder degenerierte Tangenten dürfen keine NaNs oder schwarze Materialien erzeugen.
- [ ] UV-Spiegelung, harte Normalenkanten, DFM-Skinning und mehrere UV-Sets mit Referenzmodellen testen.
- [ ] NM-Texturzuweisung und Materialparameter in LancerEdit sichtbar und validierbar machen.
- [ ] Fallback-Verhalten bei fehlender Normalmap oder fehlenden Tangenten dokumentieren und testen.
- [ ] Height-/Bump-Texturen erfassen: falls Freelancer-Materialien dafür Daten enthalten, Parallax Mapping als optionalen, klar vom Normalmapping getrennten Shaderpfad prototypen; echte Geometrieverschiebung nur bei belegtem Bedarf und mit LOD-/Kostengrenzen prüfen.
- [ ] Normalmap-Kompression, Mipmaps und Filterung auf schimmernde/specular Artefakte untersuchen; Material- und Texturfarbräume eindeutig festlegen.

### 3. Blur und Post-Processing

- [ ] Allgemeine Post-Processing-Pass-Schnittstelle ergänzen; SMAA und bestehende Render-Targets dabei als Referenz für Ressourcenlebensdauer und Skalierung verwenden.
- [ ] Separable Gaussian Blur als erster kontrollierter Effekt: variable Kernelbreite, Auflösungs-Skalierung und keine Farb-/Alpha-Halos.
- [ ] Verwendungsfälle getrennt spezifizieren: weicher UI-/Vorschau-Hintergrund, selektiver Tiefenunschärfe-Effekt und kamerabasierte Bewegungsunschärfe sind unterschiedliche Effekte.
- [ ] Für kamerabasierte Motion Blur Bewegungsvektoren und Disocclusion-Artefakte untersuchen; vorhandene Partikel-Motion-Blur nicht als Ersatz behandeln.
- [ ] Optionalen Radial-/Zoom-Blur für klar begrenzte Cutscene- oder Effektfälle prüfen.

### 4. Licht, Tonemapping und Bildqualität

- [ ] HDR-Zwischenpuffer, Belichtung und Tonemapping als zusammenhängenden Farbmanagement-Schritt entwerfen; sRGB-Ein-/Ausgabe und UI-Komposition berücksichtigen.
- [ ] Bloom als nachgelagerten Effekt mit Threshold, Downsampling-Pyramide und konfigurierbarer Intensität untersuchen.
- [ ] Image-Based Lighting und konsistente Umgebungsreflexionen für den PBR-Pfad prüfen.
- [ ] Screen-Space Ambient Occlusion als optionale Ergänzung bewerten; Geometrie- und Nebelartefakte gegen Vanilla-Szenen abgleichen.
- [ ] Echtzeit-Schatten (zunächst gerichtete Lichter, Kaskaden und PCF) anhand vorhandener Freelancer-Lichtdaten und der GPU-Kosten bewerten.
- [ ] Screen-Space-Reflections als optionale Ergänzung prüfen; fehlende Bildinformation an Bildschirmrändern und bei verdeckten Objekten muss über den bestehenden PBR-Fallback abgefangen werden.
- [ ] Volumetrischen Nebel und volumetrische Lichtstrahlen als eigenständige, skalierbare Qualitätstufe evaluieren; vorhandene Freelancer-Nebelparameter und Transparenzreihenfolge als Referenz verwenden.
- [ ] Forward+/Clustered Lighting gegen die aktuelle Beleuchtungsarchitektur und typische Freelancer-Szenen mit vielen Lichtern benchmarken, bevor ein Renderer-Umbau geplant wird.

### 5. Weitere Oberflächeneffekte und Renderer-Betrieb

- [ ] Decals für Einschläge, Verschmutzung und Missionsmarkierungen untersuchen; Sortierung, Z-Fighting und DFM-/Animierungsfälle abdecken.
- [ ] Shader-Varianten und Pipeline-Zustände nachvollziehbar cachen; Shader-Kompilierung darf beim Szenenwechsel keine langen Ruckler oder Backend-Fehler verursachen.
- [ ] GPU-Instancing und gezieltes Batching für wiederholte Asteroiden, Partikel und Basisobjekte anhand CPU-/GPU-Profilen bewerten.
- [ ] RenderDoc-/Debug-Marker pro Render-Pass und lesbare GPU-Fehlerdiagnostik für reproduzierbare Grafikfehler vorsehen.

### 6. Zeitliche und räumliche Rekonstruktion

- [ ] TAA erst nach stabilen Bewegungsvektoren, Kamerajitter und History-Buffer-Lifecycle evaluieren.
- [ ] Upscaling nur nach reproduzierbaren Auflösungs-/Performance-Messungen ergänzen; Native-Auflösung und SMAA bleiben verfügbar.
- [ ] Nebel, Transparenz, Partikel und HUD-Komposition in jeder zeitlichen Rekonstruktion separat auf Ghosting prüfen.

## Abnahmekriterien für jedes Paket

- [ ] Bestehende Freelancer-Materialien und Szenen bleiben visuell plausibel; Abweichungen werden mit Bildpaaren dokumentiert.
- [ ] OpenGL und weitere aktiv unterstützte Backends liefern konsistente Ergebnisse; Null-Backend und Headless-Läufe bleiben möglich.
- [ ] Resize, Alt-Tab, minimiertes Fenster und mehrfaches Öffnen/Schließen von Vorschauen hinterlassen keine GL-Fehler oder Ressourcenlecks.
- [ ] Jeder Effekt hat einen Qualitätsschalter, sinnvolle Standardwerte und einen messbaren Kosten-/Qualitätsvergleich.
- [ ] Änderungen am Engine-Renderer bleiben in einem fokussierten Patch unter `patches/` und in `patches/series` registriert.

## Backends und Priorisierung (Vorschlag, nur Aufgabenliste)

Stand der Sichtung 2026-10-12. Vorhanden sind die Backends `OpenGL` und `Null`; die Render-Schnittstellen (`IRenderContext`, `IShader`, `ITextureCube`, `IStorageBuffer` …) sind abstrahiert, die Shader liegen als HLSL vor und laufen über `LLShaderCompiler` (SPIRV-Cross). Das Spiel ist ein Weltraumspiel mit viel Transparenz (Nebel, Partikel, Triebwerke, Schilde) und wenigen Lichtquellen.

- [ ] **Zuerst die Pipeline (Abschnitte 1, 3, 4):** Render-Passes, HDR-Zwischenpuffer, Tonemapping und Bloom. Größter sichtbarer Gewinn für Sonnen, Triebwerke, Schüsse und Schilde, ohne neues Backend.
- [ ] **Forward oder Clustered Forward statt Deferred:** Transparenz und MSAA/SMAA sprechen gegen Deferred. Forward+ gegen die aktuelle Beleuchtung benchmarken.
- [ ] **Image-Based Lighting:** Cube Maps (`envcube_*` aus LN-Data) für den PBR-Pfad als Umgebungslicht prüfen.
- [ ] **Volumetrische Nebel und Soft Particles:** Nebelfelder sind ein Merkmal des Spiels; als eigene Qualitätsstufe mit Fallback.
- [ ] **Vulkan-Backend (Spike):** Prüfen, wie weit `IRenderContext` und die HLSL→SPIR-V-Kette tragen. Nutzen: Linux/Steam Deck, Mehrkern-Befehlsaufzeichnung für viele Schiffe, RenderDoc, MoltenVK für macOS. Aufwand und Risiko groß, vorher messen.
- [ ] **Direct3D 11 und Metal nur bei konkretem Bedarf:** D3D11 nur, wenn OpenGL unter Windows Probleme macht; Metal besser über MoltenVK.
- [ ] **Profil „Original-Look“** ohne Zusatzeffekte als Fallback und Referenz für Screenshot-Vergleiche erhalten.
- [ ] Offene Messung: GPU-Zeit je Pass auf Zielhardware (Abschnitt 1), bevor ein Backend-Umbau entschieden wird.
