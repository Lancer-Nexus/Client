# Vulkan-Backend: Untersuchung des Clients

**Zweck:** Bestandsaufnahme der Grafikschicht, der Render-Pipeline, der Shader und der Lichtquellen als Grundlage für einen Plan zu einem Vulkan-Backend (oder einem gleichwertigen modernen Backend). Dieses Dokument enthält Befunde und offene Fragen, **keinen Plan und keinen Code**.

**Stand der Untersuchung:** 2026-10-12, Checkout `Client` auf Commit `7a3518ce8` (Branch `codex/lancer-nexus-integration-2026-10-10`). Erweitert am selben Tag um die Sichtung der SDL_GPU-Dokumentation und der gebündelten SDL3-Version (Abschnitt 13).

**Methode und Grenzen:**

- Quelltext gelesen (`src/LibreLancer.Base`, `src/LibreLancer`, `src/LLShaderCompiler`, `src/Editor`), Shader-Dateien und Bündel-Inhalte aus dem vorhandenen Debug-Build ausgewertet.
- **Nichts gebaut, gestartet oder gemessen.** Es gibt keine Laufzeit-, Leistungs- oder Speicherzahlen. Aussagen über SDL_GPU stützen sich auf die Dokumentation und den Quelltext der gebündelten Version 3.2.0 (Abschnitt 13); Treiberverhalten und Leistung bleiben ungeprüft und sind als „zu prüfen“ gekennzeichnet.
- Pfade beziehen sich auf `Client/src/`, sofern nicht anders angegeben.

---

## 1. Kurzfassung

1. **Die Grafikschicht ist bereits abstrahiert.** Der gesamte Zugriff läuft über 11 Schnittstellen (`IRenderContext`, `IShader`, `ITexture2D`, `ITextureCube`, `IVertexBuffer`, `IElementBuffer`, `IStorageBuffer`, `IRenderTarget2D`, `IMultisampleTarget`, `IDepthBuffer`, `ITexture`). Es gibt zwei Implementierungen: `OpenGL` und `Null`. Ein neues Backend setzt an dieser Stelle an.
2. **Die Shader-Kette ist für moderne Backends schon vorbereitet.** Jede Shader-Variante liegt gebündelt als **SPIR-V, DXIL, MSL und GLSL** vor (im Debug-Build geprüft: 361 Varianten, SPIR-V insgesamt 4,5 MB). Der Compiler (`LLShaderCompiler`) ist ein Nachbau von SDL_shadercross und nutzt die Deskriptor-Räume von **SDL_GPU**. Zur Laufzeit wird bisher nur der GLSL-Teil benutzt.
3. **SDL3 ist im Projekt**, und die mitgelieferte `deps/x64/SDL3.dll` ist **SDL 3.2.0** (Release vom Januar 2025, erste stabile Fassung mit SDL_GPU) mit Vulkan-, D3D12- und Metal-Teil. Die C#-Bindungen entsprechen diesem Stand und werden für GPU nirgends benutzt. Upstream-Issue **#189 „Create SDL3_GPU render backend“** ist offen. Unter Linux wird die System-Bibliothek `libSDL3.so.0` geladen, deren Version nicht festgelegt ist.
4. **Die Rendering-API ist ein Zustandsautomat im OpenGL-Stil** (Setter am `RenderContext`, Anwendung beim Zeichnen). Für Vulkan oder SDL_GPU bedeutet das: Pipeline-Zustandsobjekte zwischenspeichern, Render-Passes aus Zielwechseln und Clears ableiten, Zustand in Zeichenaufrufen zusammenfassen. Das ist die größte Arbeit im Backend.
5. **Beleuchtung ist einfach:** höchstens 9 Lichter je Zeichenaufruf (Uniform-Array), Nebel, Ambient, keine Schatten, kein Deferred, kein HDR im Renderer. Das ist für ein neues Backend eine kleine Abhängigkeit, aber für künftige Effekte (Schatten, Cluster-Beleuchtung) eine feste Grenze.
6. **Es gibt wenige, klar benennbare GL-Lecks** außerhalb des Backends (Aufzählungen mit GL-Konstanten, Screenshot, S3TC, Fensterflags). Sie sind überschaubar.
7. **Belegte Stolpersteine für SDL_GPU (Abschnitt 13):**
   - **Bindungsnummern:** Der Vulkan-Teil von SDL_GPU 3.2.0 legt die Deskriptoren fortlaufend ab 0 an (Texturen, dann Storage-Puffer; Uniform-Puffer 0 bis n−1). Die Shader nutzen `b4`, `b5` und `t9`, die Compiler-Ausgabe passt so nicht. Ein Umnummerieren im Compiler ist nötig.
   - **Kein Kopieren im Render-Pass:** Uploads (Vertexdaten, Texturen) dürfen nicht innerhalb eines Render-Passes passieren. Der Client lädt auch während des Zeichnens (2D, Text, Streams). Das erfordert ein aufzeichnendes Backend.
   - **Feste Grenzen:** 4 Uniform-Puffer je Stufe (je höchstens 4096 Byte), 16 Texturen, 8 Storage-Puffer, 4 Farbziele, 16 Vertexpuffer und -attribute.
   - **Fehlende Zustände:** kein `ClampToBorder` (im Client ungenutzt), keine adaptive V-Sync.

---

## 2. Grafikschicht: Aufbau

### 2.1 Schnittstellen (`LibreLancer.Base/Graphics/Backends/`)

| Schnittstelle | Aufgabe | Wichtige Methoden |
|---|---|---|
| `IRenderContext` | Gerät, Zustand, Fabrik | `Init`, `ApplyState`, `ApplyViewport`, `ApplyScissor`, `ApplyRenderTarget`, `ApplyShader`, `ClearAll/ColorOnly/Depth`, `MemoryBarrier`, `Create*`, `SetCamera`, `SetTextureSlot`, `SetSamplerState`, `DrawNoVertexBuffer`, `SwapWindow`, `MakeCurrent`, `QueryFences`, `HasFeature` |
| `IShader` | Shader-Variante | `SetUniformBlock<T>(index, ref data, forceUpdate, forceSize)`, `HasUniformBlock`, `UniformBlockTag` |
| `IVertexBuffer` | Vertexdaten | `SetData`, `Draw` (mit/ohne Index, `baseVertex`), `DrawImmediateElements`, `BeginStreaming`/`EndStreaming`, `SetElementBuffer` |
| `IElementBuffer` | Indizes (16 Bit) | `SetData`, `Expand` |
| `IStorageBuffer` | Große Pufferdaten (Bones, Partikel, Strahlen) | `BindTo(binding, start, count)`, `BeginStreaming`, `EndStreaming`, `Data<T>(i)` |
| `ITexture2D` | 2D-Textur | `SetData` (Mip, Rechteck), `GetData`, `GetDataAsync`, `Dxt1` |
| `ITextureCube` | Cube Map | `SetData(face, level, …)`, 6 Flächen mit Mips |
| `IRenderTarget2D` | Render-Ziel auf Textur | `BlitToScreen`, `BlitToBuffer` |
| `IMultisampleTarget` | MSAA-Ziel | `BlitToScreen`, `BlitToRenderTarget` (Resolve) |
| `IDepthBuffer` | Tiefe/Stencil | `HasStencil` |

Die Aufzählung `GraphicsFeature` kennt `GLES`, `Anisotropy`, `S3TC`, `DebugInfo`, `LargeStorageBuffers`.

### 2.2 Zustand (`GraphicsState`, `RenderContext`)

Der `RenderContext` hält eine **angeforderte** Zustandsstruktur und das Backend die **angewendete**. Beim Zeichnen werden Unterschiede übertragen (`GLRenderContext.ApplyState`). Zustandsumfang:

`DepthEnabled`, `DepthWrite`, `DepthFunction`, `DepthRange` (Vektor min/max), `Wireframe`, `ClearColor`, `BlendMode` (16 Bit: Quellfaktor in den oberen, Zielfaktor in den unteren 8 Bit, D3D-Blendfaktoren 1 bis 11, `0` = aus), `CullEnabled`/`CullFaces`, `StencilEnabled` + `StencilTest` (Funktion, Fail/DepthFail/Pass-Operation, Referenz, Maske), `ColorWrite`, `ScissorEnabled`/`ScissorRect`, `Viewport`, `PolygonOffset`, `RenderTarget`, `Shader`.

Weitere Eigenschaften:

- **8 Textur- und 8 Sampler-Slots** (`TextureSlots`, `SamplerSlots`). `SamplerState` = Filterung (`Trilinear`, `Bilinear`, `Linear`, `Nearest`, `Anisotropic`) + zwei Wrap-Modi; Anisotropiestufe global (`AnisotropyLevel`).
- **Viewport- und Scissor-Stapel** (`PushViewport`, `PushScissor` mit Verschneidung mit dem Elternrechteck).
- **Kamera als Uniform:** `SetCamera` legt View/Projection/ViewProjection/CameraPosition ab (Block `b1`), `SetIdentityCamera` für 2D.
- **Primitive:** `TriangleList`, `TriangleStrip`, `LineList`, `LineStrip`, `Points`.
- **Clears finden mitten im Frame statt:** `ClearAll` nach der Vorbereitung, `ClearDepth` für das Stencil im SMAA-Pfad.

### 2.3 Oberflächenformate (`SurfaceFormat`)

`Bgra8`, `Bgr565`, `Bgra5551`, `Bgra4444`, `Dxt1`, `Dxt3`, `Dxt5`, `Rgtc1` (BC4), `Rgtc2` (BC5), `NormalizedByte2/4`, `Rgba1010102`, `Rg32`, `Rgba64`, `R8`, `Depth`, `Single`, `Vector2/4`, `HalfSingle`, `HalfVector2/4`, `HdrBlendable`. Die HDR-Formate sind in den Konvertierungstabellen des GL-Backends vorhanden, aber **vom Renderer nicht benutzt**.

DXT-Texturen werden auf Geräten ohne S3TC (GLES) in `S3TC.cs` softwareseitig dekodiert und als RGBA hochgeladen.

### 2.4 Plattform und Fenster

| Teil | Datei | Befund |
|---|---|---|
| Fenster mit SDL3 | `Platforms/SDL3Game.cs` | Flags `SDL_WINDOW_OPENGL | RESIZABLE | HIGH_PIXEL_DENSITY`, GL-Kontext wird vom Backend erzeugt (`GLRenderContext.Create(sdlWin)`) |
| Fenster mit SDL2 | `Platforms/SDL2Game.cs` | gleiche Struktur; Umschaltung über `librelancer_no_sdl3` |
| Swap und V-Sync | `GLSwap.cs`, `IRenderContext.SwapWindow(window, vsync, fullscreen)` | Adaptive V-Sync (`-1`) mit Rückfall |
| Kontextwahl | `GLRenderContext.CreateGLContext` | zuerst GL **4.3 Core**, dann **3.1 Core**, sonst **GLES 3.0**; Hinweis `librelancer_renderer=gles` erzwingt GLES |
| Headless | `HeadlessContext.cs` | verstecktes GL-Fenster für Werkzeuge/Tests |
| Null | `Backends/Null`, `Platforms/NullGame.cs` | ohne Grafik (Tests) |

Alle GPU-Ressourcen werden auf dem **UI-Thread** erzeugt (`GLWindow.IsUiThread()` wird in `GameResourceManager` geprüft, `QueueUIThread` überträgt Arbeit). Es gibt keinen parallelen Befehlsaufbau.

### 2.5 Streaming und Synchronisation (OpenGL-Backend)

- **`GLCycledBuffer`:** 3 Puffer im Wechsel, je Puffer ein Fence (`SyncPoint`). Dient für Uniform-Daten, `StorageBuffer`, Vertex-Streams.
- **Uniform-Blöcke:** Entweder echte UBOs mit Fences (`FencedUBO`, Standard an, abschaltbar mit `librelancer_fencedubo=0`) oder ohne.
- **Storage-Puffer:** Echte SSBOs ab GL 4.3 (`SSBO`, abschaltbar mit `librelancer_nossbo=1`); sonst Emulation über UBOs (`LargeStorageBuffers` meldet, ob groß genug).
- **Asynchrone Rückleser:** `Texture2D.GetDataAsync()` (Editor) über Fences.

Das Modell „3 Rahmen in Flug, Fences je Rahmen“ entspricht dem, was Vulkan oder SDL_GPU ohnehin verlangen.

---

## 3. Shader-Kette

### 3.1 Ablauf

```text
*.hlsl + *.txt  ──►  LLShaderCompiler (DXC → SPIR-V → SPIRV-Cross)  ──►  *.bin (zstd)  ──►  eingebettete Ressource
                                                                         │
 ShaderBundle.Get(flags)  ◄── AllShaders.* ◄── Laufzeit: FromResource<T>(name + ".bin")
```

- **Eingabe:** HLSL (`vertex … hlsl`, `fragment … hlsl`, `feature NAME bit`). Includes mit `#pragma include (…)` in `Shaders/includes/`.
- **Makros:** `UNIFORM_SPACE` und `TEXTURE_SPACE` werden vom Compiler je Stufe gesetzt (`LLShaderCompiler/DXC.cs`):

  | Stufe | `TEXTURE_SPACE` | `UNIFORM_SPACE` |
  |---|---|---|
  | Vertex | `space0` | `space1` |
  | Fragment | `space2` | `space3` |

  Das ist exakt die Zuordnung der SDL_GPU-Regeln (Texturen/Storage-Puffer und Uniform-Puffer je Stufe in eigenen Sets).
- **Vertex-Eingänge:** `[[vk::location(n)]]` an allen Eingängen (61 Fundstellen), also bereits Vulkan-konform.
- **Bündelformat:** Signatur `LLSHDR`, Version 1, Merkmalsmaske, Liste (Merkmale, Offset, Länge), je Eintrag ein Binär-Header `shdr` mit Längen für SPIR-V, DXIL, MSL, GL (`ShaderBytecodes.cs`). `GetSPIRV`, `GetDXIL`, `GetMSL` sind vorhanden und **werden nirgends aufgerufen**; nur `GetGLSL` wird benutzt (`GLShader.cs`).
- **GL-Ausgabe:** GLSL 140 (GL 3.1). Einschränkungen laut `LLShaderCompiler/README.md`: `cbuffer` nur als Feld von entweder `vec4` oder `ivec4` (keine Mischung), Storage-Puffer nur `StructuredBuffer`, über std140-UBO emuliert.
- **Build:** `Shader.targets` ruft den Compiler vor dem Build auf und braucht `dxc` und `spirv-cross-c-shared` als native Programme (`bin/builddeps`). Auf arm64-Linux ist DXIL nach README nicht baubar.

### 3.2 Inhalt der Bündel (aus dem vorhandenen Debug-Build)

| Bündel | Varianten | SPIR-V gesamt |
|---|---:|---:|
| `Basic_FVF` | 256 | 3257 KB |
| `PBR` | 32 | 409 KB |
| `Basic_PositionColor` / `Basic_PositionTexture` | je 16 | je rund 190 KB |
| `Basic_Skinned` | 16 | 226 KB |
| Alle übrigen 19 Bündel | 1 bis 2 | je 2 bis 25 KB |
| **Summe (24 Bündel im Spiel)** | **361** | **4,5 MB** (DXIL 6,5 MB, MSL 3,1 MB, GLSL 2,5 MB) |

Dazu kommen die Bündel der Basisbibliothek (`FSTint`, `Shader2D`, drei SMAA-Stufen) und des Editors (Grid, Normals, EnvMapTest, ImGui, AdvGradient).

### 3.3 Bindungsmodell der Shader

**Uniform-Blöcke (Bindungsnummer in der HLSL-Registrierung):**

| Nr. | Block | Stufe | Inhalt |
|---:|---|---|---|
| `b0` | `Transforms` | Vertex | `World`, `NormalMatrix` |
| `b1` | `Camera` | Vertex (und Fragment in PBR) | `View`, `Projection`, `ViewProjection`, `CameraPosition` |
| `b2` | `Lighting` | Vertex und Fragment | Nebel, Ambient, `LightCount`, 9 × `Light` (96 Byte), siehe 5 |
| `b3` | Materialparameter | Fragment | je Shader (`MaterialParameters`, `PBRParameters`: Dc, Ec, Oc, Metallic, Roughness, …) |
| `b4` | `MaterialAnim` | Vertex (`Basic_FVF`, `Basic_Skinned`, `Basic_Position*`) | UV-Animation |
| `b5` | `TexCoordSelectors` | Fragment (`Basic`, `PBR`) | welche UV-Menge je Textur |

**Texturen:** `t0` Dt (Diffuse), `t1` Et (Emission), `t2` Nt (Normal, PBR) bzw. Nt in Basic, `t3` Env-Cube (Basic) bzw. Metallic (PBR), `t4` Bt (Bump/Zweite Textur) bzw. Roughness (PBR). Je Textur ein `SamplerState` mit gleicher Nummer im selben Raum (`s0` … `s4`). Vertex-Storage: `t9` (`Bones`, `Particles`, `BeamPoints`).

**Befund für SDL_GPU (aus dem Quelltext der gebündelten Version belegt, Abschnitt 13.5):** Die Bindungsnummern müssen je Stufe fortlaufend bei 0 beginnen. Die Shader nutzen Nummern bis `b5` und `t9` und lassen je nach Variante Lücken (unbenutzte Texturen entfallen). Ohne Umnummerierung stimmt das Layout des SDL_GPU-Pipeline-Objekts nicht mit dem SPIR-V überein. Bei Vulkan direkt gilt diese Einschränkung nicht.

### 3.4 Merkmale und Varianten

| Bündel | Merkmale (Bitnummer in `*.txt`) |
|---|---|
| `Basic_FVF` | `VERTEX_LIGHTING`, `ALPHATEST_ENABLED`, `TEX2_ENABLED`, `FADE_ENABLED`, `NORMAL_MAP`, `VERTEX_DIFFUSE`, `VERTEX_TEXTURE2`, `ENVMAP` (8 Bit = 256 Varianten) |
| `PBR` | `NORMALMAP`, `METALMAP`, `ROUGHMAP`, `ET_ENABLED`, `VERTEX_TEXTURE2` (32 Varianten) |
| `Basic_Skinned`, `Basic_Position*` | 4 Bit |
| `Atmosphere`, `Detail*Material`, `NebulaMaterial` | 1 Bit |

**Zu beachten:** Im Code (`BasicMaterial.ShaderFeatures`) heißt das Normalmap-Merkmal `NORMALMAP`, in `Basic_FVF.txt` `NORMAL_MAP`. Die Zuordnung läuft über die Bitposition, nicht den Namen. Vor einer Änderung der Merkmalslisten prüfen.

---

## 4. Render-Pipeline je Frame

### 4.1 Hauptpfad im Spiel: `SystemRenderer.Draw` (`LibreLancer/Render/SystemRenderer.cs`)

Reihenfolge der Schritte:

| Nr. | Schritt | Ziel / Zustand |
|---:|---|---|
| 0 | optional `AntialiasTarget` anlegen (MSAA oder SMAA), `PushViewport`, `PushScissor` | `RenderTarget = msaa` |
| 1 | `SetCamera`, Nebel-Prüfung (`CheckNebulae`), Übergang in den Nebel? | – |
| 2 | **Vorbereitung auf der CPU:** alle Objekte `PrepareRender`; `BonesBuffer` und `QuadBuffer` streamen | Storage-Puffer |
| 3 | `ClearAll` (Farbe: Nebelfarbe, Systemhintergrund oder Schwarz) | Farbe + Tiefe (+ Stencil) |
| 4 | Frame-Start Debug, Commands, FX-Pool, Polylines, Billboards | – |
| 5 | **Depth-Vorpass** der Objekte (`ColorWrite = false`, `DepthFunction = Less`) | nur Tiefe |
| 6 | Objekte, Asteroidenfelder, Nebel in `CommandBuffer` einreihen | – |
| 7 | **Opak-Pass** `Commands.DrawOpaque` | Tiefe an, Farbe an |
| 8 | **Sternenhimmel** (`DepthRange = (1,1)`, Kamera-Z-Austausch bei Thorn-Kameras), Frustum-Culling je Modellteil, `DrawImmediate` | Tiefe auf Fernebene |
| 9 | Nebel-Übergang (`RenderFogTransition`) | Blend auf Bild |
| 10 | `OpaqueHook` | – |
| 11 | **Transparent-Pass** `Commands.DrawTransparent` (nach `z` sortiert, `DepthWrite = false`) | Blend |
| 12 | Physik-Debug, Debuglinien (`DebugRenderer`) | – |
| 13 | **Auflösen:** MSAA-Blit zum Bildschirm bzw. zu `RenderTarget2D`, oder SMAA (3 Pässe) | Ausgabe |

`CommandBuffer` fasst bis zu 16384 Befehle (`MAX_COMMANDS`) und 16384 Transparente ein. Je Befehl: Material, Welt-Matrix-Handle, Lichter (Kopie), Vertexpuffer, Primitive, Bereich, Schicht, Tiefe. `RenderMaterial.Use` setzt Uniform-Blöcke, Texturen, Sampler und Blendmodus, danach folgt der Zeichenaufruf.

### 4.2 Antialiasing (`LibreLancer.Base/Graphics/AntialiasTarget.cs`)

| Modus | Umsetzung | Anforderungen an ein Backend |
|---|---|---|
| `None` | direkt | – |
| `MSAA2x/4x/8x` | `IMultisampleTarget` mit Blit (Resolve) | Multisample-Farbe und -Tiefe, Resolve nach Textur oder Fenster |
| `SMAALow…Ultra` | `RenderTarget2D` + `DepthBuffer` (mit Stencil) + zwei weitere Ziele; 3 Pässe (Kantenerkennung → Gewichte → Nachbarschaftsmischung); Texturen `AreaTex`, `SearchTex` (DDS) | **Stencil**, zwei Ziele mit gemeinsamem Tiefen-/Stencil-Puffer, Sampler Linear/Point mit Clamp, Vollbilddreieck |

`MaxAntialias` hängt vom Gerät ab (`GL_MAX_SAMPLES`), Rückfall `SMAAUltra`. Die Stencil-Zustände werden dabei aktiv gesetzt (`Always/Replace`, dann `Equal/Keep`).

### 4.3 2D, UI und Text

| Teil | Datei | Befund |
|---|---|---|
| 2D-Batcher | `Renderer2D.cs`, `DrawList2D.cs` (730 Zeilen) | Sprites und Rechtecke, Scissor-Stapel, eigener Shader `Shader2D` |
| Text | `Text/RichTextEngine.cs`, `Text/BuiltRichText.cs`, native `libblurgtext` | Glyphen als Texturen, gezeichnet über 2D |
| UI-Widgets mit 3D | `Interface/Widgets/Widget3D.cs`, `ModelView.cs`, `CharacterView.cs` | eigene `RenderTarget2D` je Widget |
| Offscreen im Editor | `Viewport3D`, `PreviewRenderer`, `SystemMap`, `SunImmediateRenderer`, `ModelImageRenderer` | `RenderTarget2D` in verschiedenen Größen |

Die 2D-Ebene nutzt dieselbe Zustandsmaschine (`Set2DState(cull, depth)`).

### 4.4 Sonderpfade und Spezialfälle

| Pfad | Datei | Besonderheit |
|---|---|---|
| Sonne | `SunRenderer.cs`, `SunRadial`, `SunSpine` | Strahlen und Kreisverlauf als eigene Shader (Blendmodus nicht im Detail geprüft) |
| Nebel (Außen/Innen) | `NebulaRenderer.cs` (574 Zeilen), `NebulaMaterial`, `NebulaInterior`, `NebulaExtPuff` | Puffs über `QuadBuffer`, Nebelfarbe/Übergang, Blitzlicht als Lichtquelle |
| Asteroiden | `AsteroidFieldRenderer`, `AsteroidBand`, `AsteroidCubeMesh` | Feld aus Würfelzellen mit vielen Instanzen (Zeichenaufruf-Zahl nicht gemessen) |
| Partikel | `Fx/ParticleEffectPool`, `ParticleEffectRenderer`, `Materials/ParticleMaterial.cs`, Shader `Particle` | `StructuredBuffer<Particle>` an Bindung 9 (`BindTo(9, start, count)`), Vertex-ID im Shader (`SV_VertexID`), gezeichnet über den `CommandBuffer`; Grenze über `LargeStorageBuffers` |
| Strahlen | `BeamsBuffer`, `ParticleBeam` | `StructuredBuffer<BeamPoint>` |
| Polylinien | `PolylineRender`, `LineRenderer` | `PolygonOffset = (1,1)` zeitweise (Linien über Flächen) |
| Skinning (Figuren) | `CharacterRenderer`, `DfmSkinning`, `Basic_Skinned` | `StructuredBuffer<float4x4> Bones`, Puffer 65536 × 64 Byte |
| Planeten | `Atmosphere`, `DetailMapMaterial`, `Masked2DetailMapMaterial`, `IllumDetailMapMaterial`, `DetailMap2Dm1Msk2Pass` | Mehrpass-Materialien |
| Zonen | `ZoneVolume` | Volumen |
| Karte | `Navmap` | eigene Kartenansicht |
| Debug | `PhysicsDebug`, `DebugRenderer` | Linien |

---

## 5. Lichtquellen und Beleuchtung

### 5.1 Datenmodell

| Typ | Datei | Inhalt |
|---|---|---|
| `LightKind` | `LibreLancer.Data/GameData/LightKind.cs` | `Directional` (0), `Point` (1), `PointAttenCurve` (2), `Spotlight` (3) |
| `RenderLight` | (Render, Struktur) | Position, Richtung, Farbe, Ambient, Reichweite, Abschwächung (3 Koeffizienten), Spot: `Theta`, `Phi`, `Falloff` |
| `DynamicLight` | `Render/DynamicLight.cs` | `RenderLight` + `LightGroup`, `Active`, Farbkurve mit Periode |
| `SystemLighting` | `Render/SystemLighting.cs` | Liste `Lights`, Ambient, `FogMode`, `FogDensity`, `FogColor`, `FogRange` |
| `Lighting` | `Render/Lighting.cs` | **Pro Zeichenobjekt:** `Enabled`, Ambient, Nebel, `LightsArray` (Maske `SourceEnabled`, Verweis auf `SystemLighting`, `Nebula0`/`NebulaCount`) |
| `PackedLight`, `ShaderLighting` | `Render/RenderMaterial.cs` | Aufbau des Uniform-Blocks `b2` (Pack = 1) |

**Nebel:** `FogModes` = `None` (0, im Shader fest codiert), `Exp` (1), `Exp2` (2), `Linear` (3).

### 5.2 Ablauf

```text
Systemdaten (INI) ──► SystemLighting (Lichter, Ambient, Nebel)
                               │
 je Objekt: RenderHelpers.GetLighting(system, Mittelpunkt, Radius, LightGroup, nebula)
     ├─ Lichtgruppe stimmt? aktiv?
     ├─ Entfernungstest (Kugel + Reichweite), Spotlicht: eigener Kegeltest
     ├─ höchstens 9 Lichter (MAX_LIGHTS), Reihenfolge der Systemliste
     ├─ Nebel des Objekts überschreibt Ambient/Nebel
     └─ Blitzlicht (Nebel) als zusätzliches Licht (Nebula0)
                               │
 RenderMaterial.SetLights(shader, ref lighting)  ──►  Uniform-Block b2  (912 Byte bei 9 Lichtern: 3 × Vector4 + 9 × 96)
```

### 5.3 Auswertung im Shader (`Shaders/includes/Lighting.hlsl`)

- **Vertex-Beleuchtung** (`VERTEX_LIGHTING`, Standard) und **Pixel-Beleuchtung** (`PerPixelLighting` in den Einstellungen, `IRendererSettings.PerPixelLighting`).
- Formel Diffuse: Lambert je Licht, getrennt für Vorder- und Rückseite; Abschwächung nach D3D-Formel (`Type == 1`) oder Kurve mit Reichweite (`Type == 2`); Spot mit `Theta`/`Phi`/`Falloff`.
- **Summen werden auf 0..1 geklemmt** (kein HDR).
- PBR-Pfad (`PBR.frag.hlsl`): Cook-Torrance-Anteile mit denselben Lichtern, keine Umgebungsbeleuchtung (IBL) und keine Schatten.

### 5.4 Folgen für ein neues Backend und weitere Effekte

| Aspekt | Befund |
|---|---|
| Lichtanzahl | fest 9, Uniform-Array; keine Clusterung. `NumberOfTilesX` wird nur als Marker benutzt (`-1` im Frame, `RenderHelpers` nimmt den Nebelblitz nur dann auf); `Render/PointLight.cs` (Kommentar „Features430 only“) wird nur vom Nebelblitz (`NebulaRenderer.DoLightning`) benutzt |
| Schatten | keine; es gibt keinen Tiefen-Zielpass und kein Sampling einer Tiefentextur im Spiel |
| HDR | Formate vorhanden, Renderer nutzt 8-Bit-Farbziel; Tonemapping fehlt |
| Umgebungslicht | Cube Maps nur im Basic-Shader für Spiegelungen (`envmap_material`), kein IBL |
| Daten | Lichtgruppen, Farbkurven, Nebel und Blitze kommen aus den Spieldaten (Systemdateien) |
| Updates je Frame | pro Zeichenaufruf ein Upload von 912 Byte (bei Pixel-Beleuchtung zusätzlich im Fragment) |

---

## 6. Ressourcen und Vertexdaten

### 6.1 Vertexformate

| Format | Datei | Eingänge |
|---|---|---|
| `VertexPosition`, `VertexPositionColor`, `VertexPositionTexture`, `VertexPositionColorTexture`, `VertexPositionNormal`, `VertexPositionNormalTexture`, `VertexPositionNormalDiffuseTexture` | `Graphics/Vertices/` | feste Layouts |
| `FVFVertex` | `Utf/Vms/FVFVertex.cs` | Position (Float3), optional Normale (Float3), optional Farbe (UByte4 normalisiert), bis zu 4 UV-Mengen (Float2) |
| `DfmVertex` | `Utf/Dfm` | Skinning mit Bone-Indizes und Gewichten |
| Partikel/Strahlen | – | keine Vertexpuffer, nur Storage und `SV_VertexID` |

`VertexElement(slot, anzahl, typ, normalisiert, offset)`; `VertexElementType` hat **GL-Konstanten als Werte** (`GL_FLOAT` usw., siehe 7). Die Eingangsnummern (`VertexSlots`) entsprechen den `[[vk::location]]`-Angaben der Shader.

Tangenten werden in `Basic_FVF` als zwei `float2` auf zusätzlichen UV-Slots (`TEXCOORD1..3`, je nach `VERTEX_TEXTURE2`) übergeben. Die Normal-Map-Zweige im Shader hängen an diesem Layout.

### 6.2 Texturen

- Laden über `ImageLib` (DDS, TGA, PNG, …) und `GameResourceManager`.
- **Mip-Ketten aus der Datei**, Anisotropie global.
- Cube Maps: `DDS.GetTextureCube` mit Flächenfolge +X −X +Y −Y +Z −Z; der 2D-Pfad lehnt Cube-Dateien ab.
- Komprimierte Formate werden nativ hochgeladen, wo das Gerät S3TC kennt, sonst entpackt.
- Rückleser: `GetData`/`GetDataAsync` (Editor, `GameDataContext`), Screenshot des Fensters (siehe 7).

### 6.3 Puffer

| Puffer | Nutzung | Größen | Besonderheit |
|---|---|---|---|
| Vertex | Modelle (statisch), Streams (2D, Linien) | – | `BeginStreaming` für zyklische Puffer |
| Index | 16 Bit | – | `DrawImmediateElements` für sofortige Indizes |
| Uniform | Welt, Kamera, Licht, Material | 64 Byte bis 912 Byte | Hash-/Tag-Prüfung gegen doppelte Updates (`UniformBlockTag`) |
| Storage | Bones (65536 × 64 Byte), Partikel, Strahlen | je Rahmen neu | `GetAlignedIndex`, `BindTo(binding, start, count)` |
| `WorldMatrixBuffer` | Weltmatrizen (Zeiger-/ID-basiert) | – | nur CPU-seitig, Uniform pro Zeichenaufruf |

---

## 7. Abhängigkeiten von OpenGL außerhalb des Backends

Diese Stellen müssen vor oder mit einem neuen Backend entkoppelt werden:

| Fundstelle | Befund | Entkopplung |
|---|---|---|
| `Graphics/Vertices/VertexElementType.cs` | Aufzählung mit `GL.GL_FLOAT`, `GL.GL_UNSIGNED_SHORT`, `GL.GL_UNSIGNED_BYTE`, `GL.GL_SHORT` | eigene Werte, Umsetzung im Backend |
| `Graphics/WrapMode.cs` | Werte `GL.GL_CLAMP_TO_EDGE`, `GL_CLAMP_TO_BORDER`, `GL_MIRRORED_REPEAT`, `GL_REPEAT` | eigene Werte |
| `Platforms/SDL3Game.cs` (≈ Z. 243–260), `SDL2Game.cs` (≈ Z. 205–218) | `Screenshot` ruft `GL.ReadBuffer`, `GL.ReadPixels` direkt | Rückleser im Backend (`IRenderContext`) |
| `Graphics/S3TC.cs` | GL-Aufrufe und Software-Entkompression | Teil des GL-Backends, Dekompression als Hilfsfunktion |
| `Platforms/SDL3Game.cs`, `SDL2Game.cs`, `HeadlessContext.cs` | Fensterflag `SDL_WINDOW_OPENGL`, GL-Attribute | Fensterflags je Backend (bei SDL_GPU kein GL-Kontext) |
| `GLSwap.cs` | `SDL_GL_*`-Aufrufe | gehört in das Backend |
| `IRenderContext.MakeCurrent`, `SwapWindow` | Annahme: GL-Kontext des Fensters | in modernen Backends anders (Swapchain) |
| `GraphicsFeature.GLES`, `SupportsWireframe` | GL-spezifische Fähigkeitsabfragen | allgemeinere Abfrage |

Der restliche Client greift nicht auf GL zu (Stichprobe über das ganze `src/`: nur die genannten Dateien).

---

## 8. Was ein Vulkan-nahes Backend zusätzlich braucht

Die Punkte folgen aus der Art, wie der Client die Schnittstellen benutzt.

| Thema | Befund im Client | Folge |
|---|---|---|
| **Pipeline-Zustandsobjekte** | Zustand wird einzeln gesetzt, kombiniert erst beim Zeichnen: Shader-Variante × Blendmodus (121 mögliche Paare) × Tiefen-Test/Schreiben/Funktion × Cull × Stencil × Wireframe × Primitive × Vertexlayout × Farb-/Tiefenformat | Pipeline-Cache mit Schlüssel aus dem Zustand; Variantenzahl am Anfang aus Logs messen |
| **Render-Passes** | Zielwechsel und `Clear*` mitten im Frame (Hauptziel → SMAA-Ziele → Bildschirm) | Passgrenzen aus `RenderTarget`-Wechsel und Clears ableiten; `LoadOp`/`StoreOp` je Pass |
| **Clear mitten im Pass** | `ClearAll` nach `PrepareRender` im selben Ziel; `ClearDepth` für Stencil | als eigener Pass oder Clear-Attachment |
| **Blit/Resolve** | `BlitToScreen`, `BlitToBuffer`, `BlitToRenderTarget` mit Offset | Resolve oder Copy am Passende |
| **Tiefenbereich** | `DepthRange (1,1)` für den Sternenhimmel | Viewport `minDepth/maxDepth` |
| **Polygon-Offset** | `LineRenderer` setzt `(1,1)` | Depth-Bias im Zustand |
| **Wireframe** | `Wireframe` als Zustand (nicht in GLES) | `fillModeNonSolid` bzw. Entsprechung, optional |
| **Immediate-Indizes** | `DrawImmediateElements(Span<ushort>)` | transienter Indexpuffer |
| **Dynamische Uniform-Daten** | `SetUniformBlock` je Zeichenaufruf mit Tag-Prüfung | Ring-Puffer mit Offsets (Vulkan) oder `PushUniformData` (SDL_GPU) |
| **Storage-Puffer als Vertex-Eingabe** | `StructuredBuffer` in der Vertexstufe (`t9`) | Storage-Puffer-Zugriff in der Vertexstufe muss vom Backend zugelassen sein |
| **Sampler** | 8 Slots, feste Zustände, Anisotropie global | Sampler-Cache nach Zustand |
| **Mip-Erzeugung** | Mips kommen aus Dateien, `hasMipMaps` beim Anlegen | keine Mip-Generierung nötig, vollständige Ketten hochladen |
| **Komprimierte Texturen** | BC1–BC5 nativ | Auf Desktop-Vulkan verfügbar; auf Mobilgeräten ohne BC nicht, dort Softwarepfad wie GLES |
| **Rückleser** | `GetDataAsync`, Screenshot | Download-Puffer mit Fence |
| **Geräteverlust / Resize** | Ziele werden bei Größenänderung neu erzeugt (`AntialiasTarget`) | Swapchain-Neuaufbau, Rahmenverwaltung |

---

## 9. Backend-Optionen

| Option | Vorteile | Nachteile / Risiken |
|---|---|---|
| **A. SDL_GPU (SDL3)** | Ein Backend für Vulkan, Direct3D 12 und Metal. Shaderformate SPIR-V/DXIL/MSL **liegen schon vor**. Deskriptor-Räume des Compilers entsprechen den SDL_GPU-Regeln. SDL3 ist bereits Laufzeitabhängigkeit (Windows: 3.2.0 mitgeliefert). Upstream plant dasselbe (#189). Bindungen vorhanden. Alle Zustände des Clients haben eine Entsprechung (Abschnitt 13.6). Eingebauter Rahmenring, Fences, Cycling. | Nur SDL3 (SDL2-Pfad bleibt GL). Feste Grenzen (4 Uniform-Puffer, 16 Texturen, 8 Storage-Puffer). Kopieren nur außerhalb von Render-Passes. Umnummerierung der Shader-Bindungen nötig. Linux nutzt die System-SDL3 ohne feste Version. Strengere Hardwareanforderungen als GL (Vulkan-Features, D3D12 Feature Level). Mobil/GLES-Geräte (Raspberry Pi in Kommentaren genannt) bleiben auf GL angewiesen. |
| **B. Vulkan direkt** (eigene Bindungen oder Bibliothek) | Volle Kontrolle (Pipeline-Cache, Mehrkern-Aufzeichnung, Compute, Raytracing, Debug-Marker). Nur SPIR-V nötig. | Größter Aufwand (Speicher, Synchronisation, Swapchain, Plattformschicht je OS). Kein DXIL/MSL-Nutzen. macOS nur über MoltenVK. |
| **C. OpenGL beibehalten** | Funktioniert, verifiziert, SDL2 und SDL3. Einfachste Pflege. | Keine mehrkernfähige Aufzeichnung, macOS veraltet, kaum Raum für neue Effekte. |
| **D. Direct3D 11/12 / Metal einzeln** | – | Dritter bis vierter Pfad, keine neuen Plattformen gegenüber A. |

**Vorläufige Einordnung (Hypothese, nicht belegt):** A passt am besten zum vorhandenen Shader-Aufbau und zum Upstream-Ziel. B wäre nur sinnvoll, wenn Funktionen gebraucht werden, die SDL_GPU nicht bietet. C bleibt als Fallback und für SDL2/GLES bestehen. Die Entscheidung benötigt die Verifikation in Abschnitt 10.

---

## 10. Offene Fragen und Verifikationspunkte

### 10.1 SDL_GPU im Detail (Stand nach Abschnitt 13)

- [x] Version der Windows-Bibliothek: **3.2.0** (`release-3.2.0-0-g535d80bad`). Linux: System-Bibliothek, Version offen.
- [x] Uniform-Slots: **4 je Stufe**, je Block höchstens 4096 Byte, Pool aus 32-KiB-Puffern. Die Bindungsnummern `b4`, `b5` müssen umnummeriert werden.
- [x] Storage-Puffer in der Vertexstufe: unterstützt (`SDL_BindGPUVertexStorageBuffers`), höchstens 8 je Stufe. `t9` muss umnummeriert werden.
- [x] Texturformate: BC1 bis BC5, BC7, `D24_UNORM_S8_UINT`, `D32_FLOAT_S8_UINT`, `R16G16B16A16_FLOAT`, `R10G10B10A2_UNORM` usw. vorhanden. Die HDR-Formate des Clients haben Entsprechungen.
- [x] Blendfaktoren: alle 11 D3D-Faktoren plus Konstante; getrennte Alpha-Faktoren. Der Client benötigt nur `BlendFuncSeparate`.
- [x] Viewport-Tiefenbereich, Depth-Bias, Fill-Modus Linie, Scissor, Stencil-Referenz: vorhanden.
- [x] Rückleser: Download in Transfer-Puffer plus Fence.
- [x] GLES: SDL_GPU läuft nicht auf GLES. Geräte ohne Vulkan 1.0 mit den geforderten Features bleiben auf GL.
- [ ] Verhalten der Treiber unter Linux (Mesa, NVIDIA proprietär), Windows (D3D12 oder Vulkan), macOS: praktisch zu prüfen.
- [ ] Mindestversion von SDL3 für Linux festlegen und beim Start prüfen (`SDL_GetVersion` ist gebunden).

### 10.2 Shader

- [ ] Läuft das HLSL ohne Änderung durch den SDL_GPU-Pfad (DXIL, MSL)? Alle 361 Varianten kompilieren sich schon zu SPIR-V; Laufzeitprüfung steht aus.
- [ ] `[[vk::location]]`-Zuordnung gegen die SDL_GPU-Vertexattribute (Offset/Format) abgleichen.
- [ ] Fließkomma-Genauigkeit und `discard` im Basic-Shader (Alpha-Test-Variante) gegen GL vergleichen.
- [ ] Merkmalsnamen (`NORMALMAP` im Code, `NORMAL_MAP` in `Basic_FVF.txt`) abgleichen.

### 10.3 Architektur und Messung

- [ ] Anzahl Zeichenaufrufe und Zustandswechsel in repräsentativen Szenen (Raumstation, Nebel, Kampf, Asteroidenfeld, Basis) messen. Daraus die Größe des Pipeline-Cache und den Nutzen der Mehrkern-Aufzeichnung ableiten.
- [ ] Anzahl unterschiedlicher Zustandskombinationen (Pipeline-Schlüssel) je Szene.
- [ ] Zeit in `Shader`-Erzeugung beim Szenenwechsel (heute: Bündel werden beim Start kompiliert).
- [ ] Speicherbedarf der Puffer (Bones 4 MB je Rahmen, Partikel) bei 2 bis 3 Rahmen in Flug.
- [ ] Verhalten bei Fenstergrößenänderung, Vollbildwechsel, minimiertem Fenster (Passstruktur).
- [ ] Verhalten der Editor-Werkzeuge (ImGui-Renderer, `RenderTarget2D` in vielen Größen).

### 10.4 Funktionale Abhängigkeiten, die neue Effekte betreffen

- [ ] HDR und Tonemapping: Farbziel `HalfVector4`/`HdrBlendable` im Backend vorsehen, UI-Komposition im Nachlauf.
- [ ] Schatten: Tiefentextur als Eingabe (Sampler mit Vergleich) — bisher nicht vorhanden.
- [ ] Cluster-/Tile-Beleuchtung: Storage-Puffer für Lichtlisten in der Fragmentstufe — heute nur Vertexstufe.
- [ ] Mehrere Farbziele (MRT) für Deferred- oder G-Buffer-Techniken — heute nicht benutzt.
- [ ] Normal-, Roughness- und Metallic-Karten aus `LN-Data`: UV-Mengen und Tangenten (`docs/surface-maps.md`).

---

## 11. Eingaben für einen Plan (Vorschlag der Gliederung)

Dieser Abschnitt ordnet die Befunde nur nach Abhängigkeiten; Reihenfolge und Umfang sind Planentscheidungen.

| Block | Inhalt | Abhängig von |
|---|---|---|
| **0. Messung und Referenz** | Referenzszenen und Screenshots, Zählung der Zeichenaufrufe/Zustände, Basisdaten GL | – |
| **1. Entkopplung** | GL-Konstanten aus `VertexElementType`, `WrapMode` lösen; Screenshot/Rückleser in die Schnittstelle; Fensterflags je Backend; `GraphicsFeature` erweitern | 0 |
| **2. Backend-Grundgerüst** | `IRenderContext`-Implementierung: Gerät, Fenster/Swapchain, Frame-Schleife, Befehlspuffer, Ressourcenfabrik, leeres Bild | 1 |
| **3. Shader und Pipeline** | Laden des SPIR-V-Anteils, Bindungsmodell (Uniform-Slots), Pipeline-Cache, Blend-/Tiefen-/Stencil-Tabellen | 2 |
| **4. Ressourcen** | Texturen (BC, Cube, Mips), Puffer (Vertex, Index, Uniform-Ring, Storage), Sampler, Upload und Rückleser | 2 |
| **5. Render-Ziele** | Offscreen, Tiefe/Stencil, MSAA-Resolve, SMAA-Pässe, Passableitung aus Zielwechseln | 3, 4 |
| **6. Spielpfade** | Basic/PBR, 2D und Text, Nebel, Partikel, Skinning, Sonne, Planeten, Asteroiden | 3 bis 5 |
| **7. Editor und Werkzeuge** | ImGui-Renderer, LancerEdit-Viewports, Headless | 6 |
| **8. Abnahme** | Bildvergleich gegen GL, Leistungsvergleich, Plattformmatrix (Linux/Windows/macOS), Rückfallpfad | 6, 7 |
| **9. Danach: neue Effekte** | HDR, Bloom, Schatten, IBL, Cluster-Beleuchtung, volumetrischer Nebel (siehe `docs/renderer-roadmap.md`) | 8 |

**Parallele Datenarbeit (`LN-Data`):** Normal-, Roughness-, Metallic- und Cube-Maps sind bereits erzeugt und warten auf eine Engine-Seite (Lade-Profile, UV-Mengen, Tangenten). Ein neues Backend ist dafür keine Voraussetzung, beeinflusst aber, welche Texturformate (BC4/BC5) und wie viele Samplerslots verfügbar sind.

---

## 12. Quellen im Repository

| Thema | Datei |
|---|---|
| Schnittstellen | `LibreLancer.Base/Graphics/Backends/I*.cs` |
| Zustand | `LibreLancer.Base/Graphics/Backends/GraphicsState.cs`, `RenderContext.cs` |
| OpenGL-Backend | `LibreLancer.Base/Graphics/Backends/OpenGL/` |
| Shader-Kette | `LLShaderCompiler/`, `Shader.targets`, `LibreLancer.Base/Graphics/ShaderBytecodes.cs`, `ShaderBundle.cs`, `BytecodesBundle.cs` |
| Shader | `LibreLancer/Shaders/*.hlsl`, `*.txt`, `includes/`, `LibreLancer.Base/Shaders/`, `Editor/*/Shaders/` |
| Hauptrenderer | `LibreLancer/Render/SystemRenderer.cs`, `CommandBuffer.cs`, `RenderMaterial.cs`, `RenderHelpers.cs`, `Lighting.cs`, `SystemLighting.cs` |
| Antialiasing | `LibreLancer.Base/Graphics/AntialiasTarget.cs`, `AntialiasConfig.cs` |
| Fenster/Plattform | `LibreLancer.Base/Platforms/SDL3Game.cs`, `SDL2Game.cs`, `NullGame.cs`, `GLSwap.cs`, `HeadlessContext.cs` |
| SDL_GPU-Bindungen | `LibreLancer.Base/SDL/SDL3.cs` (493 SDL_GPU-Symbole, Stand 3.2.0) |
| Umgebungsvariablen | `docs/env-vars.txt` (`librelancer_renderer`, `librelancer_nossbo`, `librelancer_fencedubo`, `librelancer_no_sdl3`) |
| Upstream-Bezug | `docs/upstream-review-2026-10-04.md` (Issue #189), `docs/renderer-roadmap.md` |

---

## 13. SDL_GPU und die gebündelte SDL3: Sichtung der Dokumentation (Erweiterung)

### 13.1 Quellen dieser Sichtung

| Quelle | Version | Verwendung |
|---|---|---|
| `deps/x64/SDL3.dll` im Client (Zeichenketten gelesen) | **3.2.0** (`release-3.2.0-0-g535d80bad`, Windows-Build aus `C:\temp\SDL3-3.2.0`) | Bundled Version, enthält GPU-Teile Vulkan, D3D12, Metal |
| `SDL_gpu.h` aus dem Tag `release-3.2.0` (raw.githubusercontent.com, 4122 Zeilen) | 3.2.0 | maßgebliche API-Beschreibung für die gebündelte Version |
| `src/gpu/SDL_sysgpu.h`, `src/gpu/vulkan/SDL_gpu_vulkan.c` aus `release-3.2.0` | 3.2.0 | Grenzen und Bindungsmodell des Vulkan-Teils |
| SDL-Wiki `CategoryGPU` (wiki.libsdl.org) | aktuelle Fassung (3.4) | Systemanforderungen, Hinweise, FAQ |
| `WhatsNew.txt` (SDL main) | 3.2.x bis 3.4.x | Änderungen der GPU-API seit 3.2.0 |
| `README-migration.md` (SDL main) | – | nur die ersten 100 000 von 118 706 Zeichen gelesen; enthält keinen GPU-Abschnitt, der Teil zu `SDL_video.h` (Fensterflags) wurde nicht erreicht |
| Lokale Header | 3.4.14 (Flatpak-SDK) und 3.4.4 (Unreal-Kopie) | Vergleich 3.2.0 gegen 3.4 (Funktionen und Konstanten) |
| Client-Bindungen `LibreLancer.Base/SDL/SDL3.cs` | Stand 3.2.0 (kein `SDL_GetGPUDeviceProperties`, keine `FEATURE_*`-Properties) | Abgleich |

Nicht gelesen: die D3D12- und Metal-Quelltexte (nur Vulkan), das SDL_shadercross-Repository selbst, Treiberdokumentation.

### 13.2 Welche SDL3-Version läuft wo

| Plattform | Herkunft | Version |
|---|---|---|
| Windows | `deps/x64/SDL3.dll` (im Repository) | 3.2.0, fest |
| Linux | System-Bibliothek, `dllmap SDL3 → libSDL3.so.0` (`src/PublishAssets/LibreLancer.Base.dll.config`) | nicht festgelegt, abhängig von Distribution oder Steam-Laufzeit; CI installiert nur `libsdl2-dev` |
| macOS | nicht untersucht (kein SDL3 im Repository gefunden) | offen |

Folgen: Auf Linux und macOS kann eine neuere oder ältere SDL3 laufen als die, gegen die die Bindungen erzeugt wurden. Die GPU-API ist laut Header-Vergleich zwischen 3.2.0 und 3.4.14 **stabil**: 94 gegen 97 Funktionen (neu: `SDL_GetGPUDeviceProperties`, `SDL_GetGPUTextureFormatFromPixelFormat`, `SDL_GetPixelFormatFromGPUTextureFormat`), keine entfallenen Funktionen, keine neuen Aufzählungswerte. Neu in 3.4 sind vor allem Geräte-Eigenschaften (siehe 13.3).

### 13.3 Systemanforderungen und ihre Entwicklung

| Backend | 3.2.0 (Header der gebündelten Version) | 3.4 (aktuelles Wiki) |
|---|---|---|
| Vulkan | Vulkan 1.0 mit `VK_KHR_swapchain`, `VK_KHR_maintenance1`; Features `independentBlend`, `imageCubeArray`, `depthClamp`, `shaderClipDistance`, `drawIndirectFirstInstance` | zusätzlich `sampleRateShading`; Anforderungen Clip Distance, Depth Clamping, Indirect Draw First Instance und Anisotropie lassen sich per Eigenschaft beim Geräteerzeugen lockern (für Android) |
| Direct3D 12 | Windows 10 oder neuer, Feature Level 11_1 | Feature Level 11_0, **Resource Binding Tier 2** erforderlich (für Intel Haswell/Broadwell per Eigenschaft abschaltbar) |
| Metal | macOS 10.14+ mit Apple Silicon oder Intel Mac2, iOS/tvOS 13+ (A9 oder neuer); Simulatoren nicht unterstützt | gleich |

Weitere belegte Punkte:

- **Treiberwahl:** Das Gerät wählt das Backend aus den angebotenen Shaderformaten (SPIR-V, DXBC, DXIL, MSL, Metallib) und den vorhandenen Treibern; die Zeichenkette `SDL_GPU_DRIVER` (Hinweis) und die Eigenschaft `SDL_PROP_GPU_DEVICE_CREATE_NAME_STRING` erlauben die Vorgabe. Die gebündelten Shader bieten SPIR-V, DXIL und MSL an; DXBC fehlt.
- **Debug-Modus ist standardmäßig an** (`SDL_PROP_GPU_DEVICE_CREATE_DEBUGMODE_BOOLEAN` standardmäßig `true`). Für Release-Builds ausdrücklich abschalten.
- **Die API verzichtet bewusst auf neueste Funktionen** (Raytracing, Mesh-Shader: „keine kurzfristigen Pläne“). Sie ist für breite Hardware-Unterstützung ausgelegt.
- **Koordinaten:** linkshändig, NDC-Tiefe 0 bis 1 (wie Direct3D). Die Shader-Kette (`DXC`/SPIR-V) bringt das bereits mit, eine GL-Kompatibilität der Tiefe ist nicht nötig.
- **Werkzeuge:** RenderDoc (Windows/Linux), Xcode-GPU-Capture (Metal), Vulkan SDK bzw. `vulkan-validation-layers`, für Debug-Gruppen unter Windows `WinPixEventRuntime.dll` (ab 3.4).

### 13.4 Feste Grenzen (aus `SDL_sysgpu.h`, 3.2.0) gegen den Client

| Ressource | SDL_GPU 3.2.0 | Benutzung im Client | Bewertung |
|---|---:|---|---|
| Texturen mit Sampler je Stufe | 16 | 8 Slots (`TextureSlots`, `SamplerSlots`), Shader nutzen 5 | passt |
| Storage-Texturen je Stufe | 8 | keine | passt |
| Storage-Puffer je Stufe | 8 | 1 (`t9`: Bones, Particles oder BeamPoints) | passt, siehe 13.5 |
| Uniform-Puffer je Stufe | **4** | 4 je Shader, aber Nummern bis `b5` | Nummern müssen verdichtet werden |
| Größe der Uniform-Daten je Block | Pool aus 32-KiB-Puffern, gebunden mit höchstens **4096 Byte** (Vulkan-Teil, `MAX_UBO_SECTION_SIZE`) | `Lighting` 912 Byte, übrige kleiner | passt |
| Vertexpuffer | 16 | 1 | passt |
| Vertexattribute | 16 | höchstens 7 | passt |
| Farbziele je Pass | 4 | 1 | passt |
| Frames in Flug | 1 bis 3, Standard **2** (`SDL_SetGPUAllowedFramesInFlight`) | `GLCycledBuffer` mit 3 | frei wählbar |
| MSAA | 1, 2, 4, 8 | 2, 4, 8 | passt; Cube-Texturen nur 1 |
| Vertex-Attributformate | `FLOAT`..`FLOAT4`, `UBYTE4_NORM`, `SHORT/USHORT` (mit `_NORM`), `HALF2/4`, Ganzzahlen | Float, UnsignedByte (normiert), UnsignedShort, Short | passt |

### 13.5 Bindungsmodell: Befund und Folgen (belegt aus `SDL_gpu_vulkan.c`)

**Regel in SDL_GPU 3.2.0 (SPIR-V, siehe Dokumentation von `SDL_CreateGPUShader`):**

| Stufe | Set | Inhalt |
|---|---|---|
| Vertex | 0 | Texturen mit Sampler, dann Storage-Texturen, dann Storage-Puffer |
| Vertex | 1 | Uniform-Puffer |
| Fragment | 2 | Texturen mit Sampler, dann Storage-Texturen, dann Storage-Puffer |
| Fragment | 3 | Uniform-Puffer |

Der Vulkan-Teil baut die Deskriptor-Layouts allein aus den **Anzahlen** (`num_samplers`, `num_storage_textures`, `num_storage_buffers`, `num_uniform_buffers`) und vergibt die Bindungen **fortlaufend ab 0** (Texturen `0..n−1`, danach Storage-Texturen, danach Storage-Puffer; Uniform-Puffer `0..n−1`, als dynamische Uniform-Puffer). Die Bindungsnummer im SPIR-V muss damit der Reihenfolge entsprechen.

**Was der Client erzeugt:**

| Stufe | Ressource | Nummern in den Shadern | Passt zur Regel? |
|---|---|---|---|
| Vertex | Uniform | `b0` Transforms, `b1` Camera, `b2` Lighting, `b4` MaterialAnim (je nach Shader) | nein: `b4` liegt über der Anzahl; `b3` fehlt (Anzahl 4, Nummern 0,1,2,4) |
| Fragment | Uniform | `b1`, `b2`, `b3`, `b5` (PBR/Basic) | nein: `b5` |
| Vertex | Storage-Puffer | `t9` (Bones, Particles, BeamPoints) | nein: ohne Texturen in der Vertexstufe müsste die Nummer 0 sein |
| Fragment | Texturen | `t0` bis `t4`, je nach Variante mit Lücken (nicht benutzte Texturen entfallen) | nur, wenn alle bis zur höchsten benutzt sind |

Die Reflexion im `LLShaderCompiler` zählt die Ressourcen und prüft nur die **Set-Nummern** (`SpvcReflection`, Fehler `InvalidDescriptorSet`), nicht die Bindungsnummern. Die MSL-Übersetzung übernimmt die Bindungsnummern unverändert. Der GL-Teil nutzt Namen und braucht sie nicht.

**Folge:** Für SDL_GPU (und analog für D3D12 und Metal) ist eine Umnummerierung nötig, entweder im Compiler (SPIR-V neu nummerieren, vor SPIRV-Cross) oder als Tabelle je Shader im Bündel (Client-Slot → SDL-Slot). Der Client setzt Uniform-Blöcke über feste Indizes (`SetUniformBlock(2, …)`, `3`, `5`), eine Tabelle würde diese Indizes auf SDL-Slots abbilden.

**Weitere Einschränkung der Bindung:** `SDL_BindGPUVertexStorageBuffers` bindet **ganze Puffer** ohne Offset und Länge. Der Client bindet Bereiche (`BindTo(9, start, count)` für Partikel, Strahlen und Bones mit `BufferOffset`, `200` Matrizen). Bei SDL_GPU muss der Anfangsindex als Uniform in den Shader gegeben werden (neuer Uniform-Block oder im Material-Block). Das betrifft `Particle`, `ParticleBeam`, `Basic_Skinned`.

**`SV_VertexID` und `first_vertex`:** Die Dokumentation von `SDL_DrawGPUPrimitives` und `SDL_DrawGPUIndexedPrimitives` warnt ausdrücklich: `first_vertex` und `first_instance` sind **nicht** mit eingebauten Variablen wie `SV_VertexID` kompatibel. Die Partikel- und Strahlen-Shader nutzen `SV_VertexID`; die Draw-Aufrufe sollten mit `first_vertex = 0` erfolgen und den Anfang über das Uniform übergeben.

### 13.6 Zustand des Clients gegen SDL_GPU

| Zustand im Client | SDL_GPU 3.2.0 | Anmerkung |
|---|---|---|
| `BlendMode` (D3D-Faktoren 1..11, `BlendFuncSeparate(src, dst, ONE, ONE_MINUS_SRC_ALPHA)`) | `SDL_GPUColorTargetBlendState`: alle 11 Faktoren (Zero, One, SrcColor, InvSrcColor, DstColor, InvDstColor, SrcAlpha, InvSrcAlpha, DstAlpha, InvDstAlpha, SrcAlphaSaturate) plus Konstanten; getrennte Alpha-Faktoren und -Operation, Operationen `ADD`, `SUBTRACT`, `REVERSE_SUBTRACT`, `MIN`, `MAX` | Teil des Pipeline-Objekts, nicht dynamisch |
| `ColorWrite` | `color_write_mask` im Blend-Zustand (RGBA-Maske) | Teil der Pipeline |
| `DepthEnabled`, `DepthWrite`, `DepthFunction` | `enable_depth_test`, `enable_depth_write`, `compare_op` (8 Operationen) | Der Client benutzt nur `Less` und `LessEqual` (Aufzählung mit GL-Konstanten). Tiefenschreiben ist bei ausgeschaltetem Test immer aus |
| `StencilEnabled`, `Stencil` | `enable_stencil_test`, `back/front_stencil_state`, `compare_mask`, `write_mask`; Referenzwert **dynamisch** mit `SDL_SetGPUStencilReference` | Stencil nur im SMAA-Pfad |
| `CullEnabled`, `CullFaces` | `cull_mode` (`NONE`, `FRONT`, `BACK`), `front_face` | Pipeline |
| `Wireframe` | `fill_mode` (`FILL`, `LINE`) | Pipeline; keine Einschränkung durch GLES wie bei GL |
| `PolygonOffset` | `depth_bias_constant_factor`, `depth_bias_slope_factor`, `depth_bias_clamp`, `enable_depth_bias` | Pipeline (Linienrenderer: zwei Zustandsvarianten) |
| `DepthRange` | Viewport `min_depth`, `max_depth` | dynamisch, Sternenhimmel `(1,1)` möglich |
| `Viewport`, `Scissor` | `SDL_SetGPUViewport`, `SDL_SetGPUScissor` | dynamisch |
| Sampler (Filter, Wrap, Anisotropie) | `SDL_GPUSamplerCreateInfo`: Filter min/mag, Mip-Modus, Adressmodi `REPEAT`, `MIRRORED_REPEAT`, `CLAMP_TO_EDGE`, `max_anisotropy` | **kein `CLAMP_TO_BORDER`**; im Client wird `WrapMode.ClampToBorder` außerhalb der Aufzählung nicht benutzt (geprüft) |
| Primitive | `TRIANGLELIST`, `TRIANGLESTRIP`, `LINELIST`, `LINESTRIP`, `POINTLIST` | `Points` wird im Client nicht gezeichnet (nur als Standardwert in `MakeType`); `TriangleStrip` bei Partikeln und Skinning |
| MSAA | `sample_count` 1/2/4/8 an Pipeline und Textur; Auflösen über `resolve_texture` mit `STOREOP_RESOLVE` | `IMultisampleTarget.BlitToRenderTarget` wird zum Passende |
| Oberflächen | siehe 2.3: alle Formate des Clients haben eine Entsprechung | `Bgra8` → `B8G8R8A8_UNORM`, `Dxt1/3/5` → `BC1/2/3`, `Rgtc1/2` → `BC4/BC5`, `HalfVector4`/`HdrBlendable` → `R16G16B16A16_FLOAT`, `Depth` mit Stencil → `D24_UNORM_S8_UINT` oder `D32_FLOAT_S8_UINT` |
| V-Sync | `SDL_GPUPresentMode`: `VSYNC` (immer verfügbar), `IMMEDIATE`, `MAILBOX` (mit `SDL_WindowSupportsGPUPresentMode` prüfen) | **kein adaptives V-Sync** (der Client setzt `-1` für „adaptiv“ mit Rückfall); Abbildung auf `MAILBOX` oder `VSYNC` |
| Swapchain | `SDL_SetGPUSwapchainParameters` mit `SDR`, `SDR_LINEAR`, `HDR_EXTENDED_LINEAR`, `HDR10_ST2084`; Format über `SDL_GetGPUSwapchainTextureFormat` | HDR-Ausgabe wäre ohne weitere Arbeit am Backend möglich |
| Primitive Restart, Tessellation, Geometrie | nicht vorhanden | vom Client nicht benötigt |
| `MemoryBarrier` (SSBO) | entfällt, SDL_GPU synchronisiert selbst | vom Client nur als Schnittstellenmethode vorhanden |

**Dynamisch sind nur** Viewport, Scissor, Blend-Konstanten und Stencil-Referenz. Alles andere steckt im Pipeline-Objekt. Der Pipeline-Schlüssel muss also Shader, Blendmodus, Tiefen-/Stencil-Zustand (ohne Referenz), Cull, Fill, Depth-Bias, Primitive, Vertexlayout, MSAA und das Zielformat enthalten (Abschnitt 8).

### 13.7 Ablaufmodell und Folgen für die Frame-Struktur

Aus der Dokumentation (3.2.0):

1. **Befehlspuffer:** `SDL_AcquireGPUCommandBuffer`; Befehle werden gesammelt und mit `SDL_SubmitGPUCommandBuffer` (oder `…AndAcquireFence`) abgeschickt. Mehrere Befehlspuffer sind aus mehreren Threads zulässig, solange sie in der richtigen Reihenfolge abgeschickt werden.
2. **Render-Pass:** bis zu **4 Farbziele und 1 Tiefen-Stencil-Ziel**. Ändert sich die Menge der Ziele, **muss** der Pass beendet und ein neuer begonnen werden. Der Beginn eines Passes ist „relativ teuer“, möglichst wenige verwenden. Beim Beginn werden Viewport und Scissor automatisch gesetzt, am Ende wird der gesamte Zustand zurückgesetzt.
3. **Kopieren und Hochladen:** `SDL_BeginGPUCopyPass`, `SDL_UploadToGPUBuffer`/`…Texture`, `SDL_DownloadFrom…` laufen in einem **Kopier-Pass**. Laut Beschreibung von `SDL_BeginGPURenderPass` lässt sich „kein weiterer Render-Pass, kein Compute- und kein Kopier-Pass“ beginnen, solange ein Render-Pass offen ist. Uniform-Daten dürfen dagegen während eines Passes gepusht werden.
4. **Blit:** `SDL_BlitGPUTexture` ist **nur außerhalb jedes Passes** erlaubt.
5. **Cycling:** `SDL_GPUBuffer`, `SDL_GPUTexture` und Transfer-Puffer arbeiten intern als Ringpuffer; mit `cycle = true` wird bei gebundener Ressource auf die nächste freie umgeschaltet. Das entspricht dem Prinzip von `GLCycledBuffer`.
6. **Rückleser:** `SDL_DownloadFromGPUTexture` in einen Download-Transfer-Puffer, danach `SDL_SubmitGPUCommandBufferAndAcquireFence` und Abfrage mit `SDL_QueryGPUFence`; kurze Latenz in Kauf nehmen. Entspricht `GetDataAsync` und dem Screenshot.
7. **Swapchain:** `SDL_WaitAndAcquireGPUSwapchainTexture` blockiert, wenn bereits so viele Frames in Flug sind wie erlaubt (Standard 2).
8. **Fenster:** `SDL_ClaimWindowForGPUDevice` (kein GL-Kontext, das Fenster darf nicht mit `SDL_WINDOW_OPENGL` erzeugt sein; zu prüfen, ob ein Fenster mit dem Flag überhaupt beansprucht werden kann).

**Folgen für den Client:**

| Eigenschaft des Clients | Konflikt mit SDL_GPU | Lösungsansatz |
|---|---|---|
| Hochladen während des Zeichnens (`DrawList2D`, Text, `QuadBuffer`, Stream-Puffer, `DrawImmediateElements`, `Texture.SetData` zur Laufzeit) | Kopier-Pass nicht innerhalb eines Render-Passes | **Aufzeichnendes Backend:** Zeichenbefehle zunächst in eine Liste schreiben, Uploads in einen Transfer-Puffer, vor dem Render-Pass in einem Kopier-Pass übertragen und dann die Passes abspielen. Alternativ transiente Ringpuffer am Frame-Anfang füllen. Das ist die tragende Entwurfsentscheidung. |
| Zielwechsel mitten im Frame (SMAA, Widgets, Offscreen-Ziele) | Passende werden zu Pass-Grenzen | Pass-Ableitung aus `RenderTarget`-Wechseln im Backend; aufeinander folgende Befehle auf demselben Ziel in einem Pass halten |
| `ClearAll` nach der CPU-Vorbereitung im selben Ziel | Löschen als `load_op` des Passes | Befehle bis zum ersten Zeichenaufruf sammeln; `Clear` vor dem ersten Draw wird zum `load_op = CLEAR` |
| `ClearDepth` als Stencil-Clear im SMAA-Pfad | Löschen nur zu Pass-Beginn | eigener Pass für den Kantenpass |
| Zustand wird einzeln gesetzt, Zeichenaufruf wendet an | Pipeline-Objekt statt Einzelzustand | Cache nach Schlüssel (13.6); Variantenzahl zuerst messen |
| `BlitToScreen`, `BlitToBuffer` (MSAA-Auflösen, Offset) | Blit nur außerhalb von Passes; Resolve ist Teil des Passes | `STOREOP_RESOLVE` für MSAA, `SDL_BlitGPUTexture` für den Rest; Fenster-Ausgabe nach dem letzten Pass |
| `SetUniformBlock` je Zeichenaufruf, Tag-Prüfung | Push-Modell, kopiert die Daten sofort | Tag-Prüfung bleibt nützlich (weniger Pushes), Daten je Push höchstens 4096 Byte |
| Bereichs-Bindung der Storage-Puffer | ganze Puffer, kein Offset | Anfangsindex als Uniform (13.5) |

### 13.8 Dokumentierte Hinweise und Warnungen (Auswahl)

- Uniforms sind „für kleine Datenmengen“. Mehr als „eine Matrix oder zwei“ je Aufruf gehört in einen Storage-Puffer. Der Block `Lighting` (912 Byte, 9 Lichter) liegt darüber; ein Storage-Puffer oder ein Zusammenfassen über ein Frame wäre eine Optimierung, kein Muss.
- Ressourcen nicht ständig erzeugen und freigeben; Pipelines und Sampler vorab erstellen und cachen.
- Daten so früh wie möglich im Frame hochladen.
- Die häufigsten Fehler sind falsche Bindungslayouts und falsche Anzahlen in `SDL_GPUShaderCreateInfo`; Reflexion verwenden. Der Client-Compiler liefert die Zahlen schon (`ReflectedShader.NumSamplers`, …), es fehlt die Umnummerierung.
- D3D12: Nicht-Systemsemantiken werden als `TEXCOORD` behandelt (Eigenschaft für das Präfix). Der Client nutzt `POSITION`, `NORMAL`, `COLOR`, `TEXCOORDn` über `[[vk::location]]`; die Übersetzung nach DXIL erfolgt über SPIRV-Cross und prüft das in 10.2.
- Metal: Vertexpuffer 0 liegt bei `[[buffer(14)]]`; Uniform-Puffer vor Storage-Puffern.
- Texturen mit Mehrfach-Sampling: Cube, 3D und Array-Texturen nur mit 1 Sample.

### 13.9 Ergebnis der Sichtung für die offenen Punkte

| Frage aus Abschnitt 10 | Antwort |
|---|---|
| SDL3-Version der Bundles | Windows 3.2.0; Linux: System, offen |
| Uniform-Slots und Nummern `b4`/`b5` | 4 Slots; Nummern müssen verdichtet werden (13.5) |
| Storage-Puffer in der Vertexstufe | unterstützt, aber nur ganze Puffer, Nummer 0 nötig, Bereich über Uniform |
| Texturformate | alle benötigten vorhanden |
| Blendmodi | alle D3D-Faktoren vorhanden |
| Viewport-Tiefe, Depth-Bias, Wireframe, Scissor, Stencil-Referenz | vorhanden |
| Rückleser | Transfer-Puffer plus Fence |
| GLES | nicht unterstützt, GL bleibt dafür |
| V-Sync-Modi | `VSYNC`, `IMMEDIATE`, `MAILBOX`; kein adaptiv |
| Wrap-Modus `ClampToBorder` | nicht vorhanden, im Client ungenutzt |
| Hochladen im Pass | nicht erlaubt: aufzeichnendes Backend nötig |

### 13.10 Weiterhin offen nach dieser Sichtung

- [ ] **Praktisch prüfen:** minimales SDL_GPU-Programm mit den gebündelten Shadern (SPIR-V und DXIL), eine Pipeline für `Basic_FVF`, auf Linux (Mesa/NVIDIA) und Windows (Vulkan und D3D12).
- [ ] Ob ein SDL3-Fenster mit `SDL_WINDOW_OPENGL` für SDL_GPU beanspruchbar ist (sonst Fenstererzeugung je Backend).
- [ ] Wie der Compiler die Umnummerierung am besten erhält (SPIR-V-Umschreibung gegenüber Tabelle im Bündel); Auswirkung auf den GL-Teil prüfen.
- [ ] Verhalten der D3D12- und Metal-Übersetzungen (DXIL über SPIRV-Cross und DXC, MSL über SPIRV-Cross) bei den 361 Varianten.
- [ ] Mindestversion von SDL3 für Linux und macOS und deren Bereitstellung im Paket (heute nur Windows gebündelt).
- [ ] Messung: Zeichenaufrufe, Pipeline-Varianten und Uniform-Daten je Frame (10.3), erst damit lässt sich die Größe von Pipeline-Cache und Ringpuffern festlegen.
- [ ] Einfluss der 3.4-Funktionen (Features lockern, Geräte-Eigenschaften) auf die Mindestanforderungen unter Android/Raspberry Pi.
