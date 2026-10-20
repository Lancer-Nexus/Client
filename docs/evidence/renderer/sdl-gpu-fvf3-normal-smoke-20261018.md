# SDL_GPU FVF3 normal-map smoke (2026-10-18)

## Lauf

```text
DOTNET_ROOT=/opt/dotnet-10 /opt/dotnet-10/dotnet build src/lancer/lancer.csproj -c Release --no-restore -p:UseSharedCompilation=false
DOTNET_ROOT=/opt/dotnet-10 /opt/dotnet-10/dotnet src/lancer/bin/Release/net10.0/lancer.dll --sdl-gpu-smoke
```

Release-Build: 0 Fehler, 180 Warnungen.

## Laufzeitbefund

- SDL_GPU wählte `vulkan`; SPIR-V wurde unterstützt.
- Adapter: Intel Haswell über Mesa Vulkan. Mesa gab zweimal `Haswell Vulkan support is incomplete` aus.
- Synthetische Freelancer-FVF3-Daten wurden mit interleavten Position-/Normal-/UV-Feldern (3/3/2 Floats) hochgeladen. Der Vertexshader gab Position, Normale und UV weiter; der Fragmentshader bildete aus der Normale eine TBN-Basis.
- Ein 16-Bit-Indexbuffer `[0, 0, 1, 2]` wurde mit `first_index=1`, drei Indizes und `vertex_offset=3` gezeichnet. Drei degenerierte Dummy-Vertices vor dem sichtbaren Dreieck machen einen ignorierten Vertexoffset im Pixel-Readback sichtbar.
- BC5-Normaltextur, D32-Tiefenziel, `LESS`-Tiefentest, RGBA16F-Szenenziel, Scene-Copy/Tonemapping, Präsentation und Swapchain-Pixelreadback bestanden.
- Mittelpunktpixel: `(226, 214, 230, 255)`; innerhalb der für Swapchainformat und ACES-Probe gesetzten Toleranz.
- Prozessende: Exitcode 0.

## Geltungsbereich

Der Probe ist eine synthetische SDL_GPU-API-Prüfung. Er lädt keine Freelancer-Daten und prüft weder echte VMesh-Assets noch Produktionsshader, DFM-Skinning, Materialauflösung, Licht-/Schattenpass oder Spiel-/Editor-Szenen. Der Befund ist daher kein Nachweis für einen portierten Spielrenderer und keine Vulkan-Hardwarefreigabe.
