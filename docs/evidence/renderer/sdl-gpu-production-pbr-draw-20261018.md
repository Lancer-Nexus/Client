# SDL_GPU Production PBR Draw

Stand: 2026-10-18. Der SDL_GPU-Diagnosepfad lädt eine echte Shaderpermutation
aus `LibreLancer` `PBR.bin`, erzeugt daraus Shader und Pipeline, bindet
Uniforms/Texturen und prüft einen tatsächlich gezeichneten Pixel. Geometrie,
Materialwerte und Texturen sind synthetisch; es wird kein Freelancer-Datenpaket
geladen.

## Lauf

Gebaut wurde der Release-Client mit .NET 10:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  dotnet build -c Release --no-restore -m:1 -p:UseSharedCompilation=false \
  src/lancer/lancer.csproj -v:minimal
```

Der Lauf auf Mesa/Vulkan Intel Haswell:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
SDL_VIDEODRIVER=x11 src/lancer/bin/Release/net10.0/lancer --sdl-gpu-smoke
```

Ergebnis: Release-Build mit 22 Projekten und 0 Fehlern. SDL_GPU lud PBR-Variante
512 mit 3 Vertex- und 3 Fragment-Uniformblöcken sowie 2 Fragment-Samplern.
Die Pipeline wurde angelegt; ein FVF3-Dreieck verwendete die eingebettete PBR-
Vertex-/Fragmentshaderpermutation, zwei kompaktiert gebundene Sampler und
synthetische Uniformdaten. Der aus der Swapchain gelesene Produktions-PBR-Pixel
war `(148, 78, 0, 255)` und bestand den Materialfarbvergleich. Der unabhängige
BC5-/HDR-/Depth-Smoke bestand ebenfalls mit Mittelpunktpixel
`(226, 214, 230, 255)`.

Der Lauf belegt die SDL_GPU-Kompatibilität und tatsächliche Fragmentausgabe
dieser PBR-Variante. Noch offen sind echte Mesh- und Materialassets,
DFM-Skinning sowie die Einbindung der GPU-Ressourcen- und Rendercommandpfade
in `SystemRenderer` und `RenderContext`.

## Schattenkartenvariante

Der Smoke lädt zusätzlich die Produktionspermutation `PBR_VIEW_VECTOR | SHADOWMAP`
(Featuremaske 768; 3 Vertex- und 4 Fragmentuniformblöcke, 3 Fragment-Sampler).
Mit derselben Permutation, demselben gerichteten Licht und demselben Dreieck wird
eine samplebare D32-Tiefentextur zuerst auf 1 (Licht sichtbar) und danach auf 0
(Empfänger bei Tiefe 0,5 abgeschattet) gelöscht. Der Swapchain-Readback ergab
bei Tiefe 1 `(54, 33, 9, 255)` und bei Tiefe 0 `(0, 0, 0, 255)`. Beide
Prüfungen liefen auf Mesa/Vulkan Intel Haswell. Dieser A/B belegt, dass die
Schattenpermutation den Tiefentexturwert für die Lichttransmission berücksichtigt.

Die Spielpipeline besitzt daneben bereits einen separaten OpenGL-Schattenpass in
`SystemRenderer`; dieser SDL_GPU-Smoke integriert oder ersetzt ihn nicht. Der
Probe bleibt synthetisch und verwendet keine Freelancer-Geometrie oder
Szenenbeleuchtung.

## Shadow-Tiefenbereich zwischen OpenGL und Vulkan

Die gemeinsame Shadow-Shaderfunktion hat nun einen expliziten
`ZeroToOneClipDepth`-Vertrag. Der OpenGL-Spielpfad meldet `false` und behält
die Abbildung `ndc.z * 0.5 + 0.5`; Vulkan verwendet `ndc.z` unverändert. Der
SDL_GPU-Smoke setzt denselben Uniformschalter wie ein Zero-to-one-Backend und
testet mit Empfängertiefe `0,5` zwei Grenzwerte: Shadow-Depth `0,6` muss Licht
durchlassen, `0,4` muss abschatten. Der Vulkan-Readback lieferte weiter
`(54, 33, 9, 255)` und `(0, 0, 0, 255)`. Mit der früher fest eingebauten
OpenGL-Abbildung wäre `0,6` fälschlich als Schatten behandelt worden.

Der Release-Build kompilierte die Shaderpakete für alle registrierten
Permutationen und 22 Projekte ohne Fehler; der Vulkan-Smoke lief erfolgreich
auf Mesa/Vulkan Intel Haswell. Eine NAP-only-OpenGL-Szenenabnahme nach dieser
Änderung steht noch aus. Der Spielrenderer bleibt aktuell OpenGL; der
Zero-to-one-Schalter bereitet den gemeinsamen Shadervertrag für SDL_GPU vor.
