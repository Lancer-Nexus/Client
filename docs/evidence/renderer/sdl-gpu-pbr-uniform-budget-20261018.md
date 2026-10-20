# SDL_GPU PBR Uniform-Budget

Stand: 2026-10-18. Dieser Nachweis betrifft die PBR-Shaderkompilierung und
deren serialisierte SDL_GPU-Ressourcenzahlen. Er belegt keine Ausführung der
Produktionsshader im Spielrenderer.

## Änderung

- PBR-Fragmentshader erhalten den Kamera-zu-Oberfläche-Vektor als interpolierte
  Vertexausgabe. Damit entfällt der fragmentseitige Kamera-Uniformblock.
- Die sechs UV-Selektoren liegen als Floatvektoren im bestehenden
  `PBRParameters`-Block. Damit entfällt der separate `TexCoordSelectors`-Block.
- Der Client setzt `PBR_VIEW_VECTOR` für alle PBR-Materialvarianten. Schatten-
  Parameter bleiben im separaten Block.

Die ausgewählten PBR-Fragmentvarianten benötigen dadurch drei Uniformblöcke
ohne Schatten und höchstens vier mit Schatten, passend zum SDL_GPU-Limit von
vier Uniformblöcken je Shaderstufe. Die übrigen, nicht vom Client gewählten
Fallbackpermutationen bleiben im Bundle erhalten.

## Prüfung

- Release-Build von `src/LibreLancer/LibreLancer.csproj`: 21 Projekte, 0 Fehler
  und 119 Warnungen.
- Erzeugte Shaderbundles enthalten je 1024 Varianten für `PBR` und
  `PBR_Skinned`; in beiden Bundles liegen alle 512 Varianten mit
  `PBR_VIEW_VECTOR` bei höchstens vier Fragment-Uniformblöcken.
- Die Grenze wurde aus den serialisierten SPIR-V-Ressourcenzahlen der erzeugten
  Bundlevarianten ausgewertet. 128 andere Fallbackvarianten je Bundle
  überschreiten weiter vier Blöcke; der Client setzt deren Featurebit nicht.

Offen bleibt die SDL_GPU-Bindung und Ausführung dieser Shader im eigentlichen
Client-Renderer.
