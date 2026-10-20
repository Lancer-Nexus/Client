# Renderer-Pipeline: Ist-Stand und Abschlussplan

Stand: 2026-10-19. Dieser Überblick beschreibt den aktuellen Client-Overlay-Stand.
Quellcode und Laufzeitnachweise bleiben maßgeblich; ein vorhandener Schalter
allein gilt nicht als Qualitäts- oder Plattformabnahme.

## Vorhandene Funktionen

| Bereich | Stand | Nachweis / Grenze |
|---|---|---|
| Freelancer-Forward-Renderer | Vorhandener OpenGL-Renderpfad mit Depth-Prepass, opaken Objekten, Sternen/Nebeln, Transparenz und AA-Auflösung. | `SystemRenderer.Draw()` und `docs/evidence/renderer/forward-pass-order-audit-20261015.md`. Der Render-Graph beschreibt Verträge; er ersetzt noch nicht alle direkten Backend-Aufrufe. |
| Normalmaps | MAT-Normalmaps und optionale Surface-Zuordnungen für Meshes und DFM; UV-Auswahl, Tangentenaufbau, Cache und Laufzeitoptionen sind vorhanden. | Viewer-A/B für Schiffe, Basen und DFM liegen unter `docs/evidence/renderer/`. Spielbildqualität und Kosten über weitere Szenen bleiben offen. |
| Metallic/Roughness | Ergänzende Karten sind in Materialauflösung und PBR-Shader eingebunden; DFM besitzt einen skinned PBR-Pfad. PBR berücksichtigt nun auch lineare Vertex-Tints und `Oc`-Materialopazität. | Kartenwirkung ist asset- und materialabhängig. Tinted/transparent PBR-Materialien, vollständige Asset-Abdeckung und systematische Spielabnahme fehlen noch. |
| Cubemaps / Reflexion | PBR bindet GGX-vorgefilterte Specular-Cubes und cosine-konvolvierte diffuse Irradiance. DDS-Cubemaps unterstützen zusätzlich lineare DX10-RGBA16F-Daten für HDR-Werte über 1. Die drei erzeugten Umwelt-Cubes und Metadaten liegen in `Data/NAP/Surface/` und `Data/SDK/fx/SurfaceMaps.ini`. | Ein NAP-only-LancerEdit-A/B lädt `envcube_sun` als `TextureCube`; im LI-Elite-Modellcrop ändern sich 13.492/99.000 Pixel, der Hintergrund bleibt pixelidentisch. Der Effekt ist subtil. Retail-Orientierung, Materialkalibrierung und Ingame-/Hardwareabnahme bleiben offen. Siehe [HDR-Cubemap-Laufzeittest](evidence/renderer/hdr-cubemap-live-ab-20261018/README.md). |
| Legacy-Environment-Maps | Bestehende Freelancer-Cubemap-Reflexionen lassen sich unabhängig vom PBR-IBL per Einstellung oder `--render-legacy-environment-maps=on|off` umschalten. Der Standard bleibt `on`, damit ältere Materialien ihr bisheriges Verhalten behalten. | Beide Startwerte laden bis ins Intro. Der gleiche Kamera-A/B-Bildvergleich fehlt noch; externe Bildschirmaufnahmen waren in der letzten Sitzung schwarz. |
| HDR und Tonemapping | Linearisiertes Scene-Target, ACES-Tonemapping, linearisierte PBR-Albedo-/Licht-/Emissionswerte und exakte sRGB-Ausgabe im PBR-LDR-Pfad. System- und Nebel-Clearfarben werden ebenfalls vor dem HDR-Clear linearisiert; LDR bleibt unverändert. Ein Source-Audit hat alle 19 Ingame-Fragmentshader und den Postprocess-Ausgang geprüft; 2 Farbraum- und 21 Ambientlicht-Regressionstests bestehen. | Retail-Farbannahmen, visuelle Gleichheit zwischen Legacy/PBR, transparente Effekte, Fog und zusammengesetzte Ingame-Szenen brauchen weiterhin feste Bildvergleiche, Original-Client-Referenz und Hardware-GPU-Abnahme. HDR/LDR-Vergleiche: `docs/evidence/renderer/postprocess-four-mode-ab-20261017/` und der same-session-Hi02-Lauf `docs/evidence/renderer/hdr-ldr-hi02-live-ab-20261018/`; letzterer zeigt in dieser Legacy-Szene nur geringe Pixelabweichung und ersetzt keine Material-/Emissions-Referenz. Shaderaudit: `docs/evidence/renderer/hdr-shader-color-contract-audit-20261018.md`; Clear-Farbvertrag: `docs/evidence/renderer/hdr-clear-color-contract-20261018.md`. |
| Bloom | HDR-Extraktion mittelt jetzt ein vollständiges 4×4-Quellfenster in Viertelauflösung; aufrundende Zielmaße behalten Randpixel bei ungeraden Fenstergrößen. Basis-, mittlere und breite Blur-Stufen sowie Schwelle, Radius, Auflösung und Kernel sind konfigurierbar. | Die Extraktion beseitigt das bisherige Unterabtasten der zentralen vier Texel. Der Code führt alle drei Stufen aus. NAP-only M01A-Läufe am 19.10. vergleichen Bloom ein/aus und schalten in einem dritten Lauf zusätzlich Partikeleffekte aus. Der große Ring verschwindet mit den Explosionspartikeln; NAP-Fuse- und ALE-Daten verknüpfen ihn mit dem `planetflare`-Flashbang der Freeport-7-Explosion und grenzen ihn von Bloom ab. Die bewegten Frames sind nicht pixelgenau, die einzelne Flashbang-Instanz bleibt offen. Siehe [`NAP M01A Bloom-/Partikelmatrix`](evidence/renderer/nap-full-runtime-20261019/README.md). GPU-Kosten bleiben offen. |
| Antialiasing | SMAA-Pässe und MSAA-Auflösung; HDR/LDR-Kombinationen sowie Resize und Minimize/Restore wurden auf OpenGL/Mesa geprüft. | Andere Backends/Treiber, wiederholte Lifecycle-Zyklen und Fehler-Injektion stehen aus. |
| Nomad-Gradient | Der HLSL-Shader begrenzt den View-/Normal-Dot-Product nun wie der frühere LibreLancer-Shader auf `[0, 1]`, anstatt den gesamten signierten Bereich auf `[0, 1]` umzurechnen. | Produktionsshader-Build und gezielter Nomad-Schiff-A/B mit Retail-NAP-Daten stehen aus. Quellvergleich: `docs/evidence/renderer/nomad-gradient-legacy-parity-20261018.md`. |
| GPU-Readback | Asynchrones OpenGL-Textur-Readback verwendet PBO und Fence. Synchron liest Desktop-GL die angeforderte Mip-Ebene mit `glGetTexImage` und kopiert die Region in den Zielbereich; GLES verwendet ein temporäres FBO und `ReadPixels`. Pack-Alignment und PBO-Bindung werden wiederhergestellt. | Ein Release-Runtime-Probe mit Mip 1, Teilrechteck und Zieloffset besteht auf Mesa/OpenGL mit Intel HD 4600. Komprimierte Formate werden ausdrücklich abgelehnt. GLES-Lauf, Fehler-Injektion und Editor-Export bleiben offen. Siehe `docs/evidence/renderer/gl-texture-readback-smoke-20261018.md` und `docs/evidence/renderer/gl-texture-async-readback-lifecycle-20261018/`. |
| Postprocess-Lifecycle | Postprocess-Pässe werden in umgekehrter Ausführungsreihenfolge entsorgt; bei einem Fehler läuft die Freigabe aller übrigen Pässe weiter. Der Plan ist idempotent entsorgbar und meldet Fehler gebündelt. | Regressionstests decken Reihenfolge, Fehlerfortsetzung und wiederholtes Dispose ab. Fehler im eigentlichen Backend-Ressourcenfreigabepfad und Kontextverlust sind weiter offen. |
| Systemrenderer-Lifecycle | `SystemRenderer.Dispose()` versucht die Freigabe aller Frame-/Effekt-/Target-Ressourcen und GPU-Timerabfragen auch dann, wenn einzelne Freigaben fehlschlagen; Fehler werden gebündelt. Wiederholtes Dispose ist wirkungslos. | Fehlerfortsetzung der allgemeinen Bereinigungsroutine ist unit-getestet. Native GL-Fehlerinjektion und Kontextverlust bleiben offen. Siehe `docs/evidence/renderer/system-renderer-cleanup-20261018/`. |
| RenderContext-Lifecycle | SDL2/SDL3 geben den RenderContext vor dem nativen Kontextabbau frei. Nach Abschluss der GPU-Fences werden Shader, SMAA-Texturen, Fullscreen- und Renderer2D-Puffer, Font-Texturen sowie der unmanaged 2D-Pool auch bei Einzel-Fehlern bereinigt. | Headless-Regression prüft Fence-Drain, Wiederholbarkeit und bekannte Kontext-Ressourcen. GL-native Fehlerpfade und Kontextverlust bleiben offen. Siehe `docs/evidence/renderer/render-context-shutdown-20261018/`. |
| Shader-Cache über Context-Wechsel | Built-in-Shaderbündel werden je `RenderContext` neu bezogen; `AsteroidBandMaterial` hält keinen globalen Shaderverweis mehr. | Headless-Regression prüft den Wechsel nach dem Schließen des ersten Shader-Scopes. Native GL-Kontextverlust- und Gerätewechselabnahme bleiben offen. Siehe `docs/evidence/renderer/render-context-shader-cache-20261018.md`. |
| Shader-Kompilierung | Alle 25 Shader-Bundles und die definierten Feature-Permutationen kompilieren mit dem im Repository enthaltenen DXC. | Belegt den HLSL-/SPIR-V-Compilerpfad, nicht die Pipeline-Erstellung oder das Zeichnen auf realen Treibern. Siehe `docs/evidence/renderer/shader-bundle-dxc-audit-20261018.md`. |
| Dynamische Lichter und Schatten | Persistente und kurzlebige Lichter werden priorisiert; `lgt_effect`-Lichter von fliegenden Projektilen folgen nun dem Projektilalter und seiner Position. Ein ausgewähltes Richtungslicht läuft durch Shadow-Map-Pass und 3×3-PCF. Alpha-Test-Texturen werden auch in PBR-Depth-/Shadow-Pässen anhand eines Material-Uniforms ausgeschnitten; blendtransparente Materialien werfen keine Schatten. Lichtprojektion ist im Licht-XY-Raum verankert und texelgesnappt; Mesh-Cullingradien berücksichtigen `RenderScale`. | Unit-Tests decken Lichtkurvenzeit, Position, Gruppe und ungültige Radien für Projektil-Lichter ab. Hi02 belegt Pass, Lichtauswahl und Live-Schaltung auf einer OpenGL-GPU. Kamera-Fit-/Stabilitäts- und RenderScale-Radiustests bestehen. Ein NAP-only-Hi02-A/B auf Intel HD 4600 bestätigt den aktiven Pass bei Bias 0 und 0,02; der Hull-Unterschied ist subtil und kalibriert keinen empfohlenen Wert. Eine Ingame-Abnahme von Waffenfeuer-/Projektillichtern, Offscreen-Castern, weiteren Materialien/Szenen und GPU-Kosten bleibt offen. Siehe `docs/evidence/renderer/shadow-map-hi02-live-toggle-20261017/`, `docs/evidence/renderer/shadow-camera-stability-20261018.md` und `docs/evidence/renderer/scaled-model-culling-20261018.md`. |
| Diagnose | CPU-Framezeitprofile, asynchrone OpenGL-GPU-Passzeiten, Bloom-/Shadow-Passzeiten, Ziel-/Speicherschätzungen und CSV-Ausgabe. Environment-Cubemaps mit mehreren Catalog-Aliasen werden im geschätzten GPU-Texturspeicher nur einmal gezählt und bei Bereinigung auch nur einmal freigegeben. | Langzeitprofile und plattformübergreifende Vergleichsbaselines fehlen. Einzelne asynchrone Samples sind keine FPS-Baseline. |

## Neueste Ingame-Beobachtung

Der NAP-only-Cityscape-Lauf vom 2026-10-18 zeigte vor dem Sky-Fix einen
schwarzen Himmel. Ursache war der Stars-/Nebula-Pass: Ohne Depth-Prepass blieb
`Less` aktiv, wodurch THN-Sphären mit Tiefenwert 1 gegen den Clear-Depth
verworfen wurden. Patch `2353-fix-sky-sphere-depth-test.patch` rendert diesen
Pass mit `LessEqual` und stellt Tiefenfunktion und Tiefenbereich danach wieder
her. Der Release-Lauf zeigt nun den blau-violetten Cityscape-Himmelsdom. Vorher-
und Nachherbild, Manifest-ID `lancer-nebula-ambient-smoke-20261018` und
NAP-Hashprüfung stehen unter
[`Sky-Sphere Depth Fix`](evidence/renderer/sky-sphere-depth-20261018/README.md).
Die Kamera war zwischen den Bildern weitergelaufen; Perspektive und Ausschnitt
sind daher nicht identisch.

Ein separater FP7-Intro-Loglauf meldet, dass `sun.sph` nicht alle sechs Seiten
enthält. Die NAP- und Retail-Dateien sind bytegleich und enthalten nur eine
Seite mit Materialname `none`; der `SphFile`-Pfad erzeugt dafür keine
Cubemap-Sphere-Geometrie. Der sichtbare Stern und Lens Glow werden separat über
`StarArch.ini` und `SunRenderer` gezeichnet. Ein früherer NAP-only A/B zeigte
eine sichtbare Lens-Glow-Schalterwirkung. Der `sun.sph`-Hinweis belegt daher
keine fehlende sichtbare Sonne oder Lens-Glow-Funktion; Details stehen unter
[`Sun sphere asset and renderer path`](evidence/renderer/sun-sph-placeholder-20261018.md).

Der NAP-only FP7-Intro-Vergleich vom 2026-10-17 zeigt im `Original`-Profil
und mit den zusätzlichen Renderfunktionen aus dieselben gesättigt blauen
Polygonflächen auf der Station. Die Flächen bleiben auch bei ausgeschalteten
Surface-Maps sichtbar; weitere Aufnahmen mit deaktivierten Schatten, Depth
Prepass und HDR entfernen sie ebenfalls nicht. Die Ursache ist damit noch
nicht isoliert. Vor einem Eingriff in Legacy-Materialien oder Mesh-Daten fehlt
der Vergleich derselben Szene mit dem Retail-Client. Aufnahmen und Optionen:
[`FP7 intro feature matrix`](evidence/renderer/fp7-intro-feature-matrix-20261017/README.md).

## FP7-System: NAP-Lauf mit HDR-Optionen (2026-10-18)

Ein aktueller Debug-Client-Lauf wurde in einem isolierten Xephyr-Display mit
OpenGL 4.5/Mesa llvmpipe und dem verifizierten 14er-NAP-Manifest gestartet.
Das Spiel lud Daten und UI, betrat `FP7_system` und zeigte im pausierten Frame
Station und Nebel. HDR, Bloom, Schatten, SSAO, Sun Ambient, Lens Glow, Soft
Particles, Normalmaps, PBR-Maps, Umweltreflexionen, diffuse IBL und Legacy-
Environment-Maps waren aktiviert. Das ist ein Laufzeitbeleg für den kombinierten
Optionssatz, aber kein Einzelnachweis jeder Funktion und kein HDR/LDR-A/B.
Schwarze Randbereiche entsprechen dem expliziten THN-Letterboxing (518 px
Szenenviewport, 101 px Rand bei 1280×720); daraus ergibt sich kein bestätigter
Viewportfehler. Aufnahme und
Manifestbedingungen: [`FP7 HDR NAP runtime`](evidence/renderer/fp7-live-hdr-nap-20261018/README.md).

## HDR-Cubemap-Laufzeitstatus (2026-10-18)

`DDS.FromStream` akzeptiert für Cubemaps nun DX10 `R16G16B16A16_FLOAT` mit
einer 2D-Cube-Arrayebene. Die HalfVector4-Texturen bleiben linear; Basic- und
PBR-Shader überspringen für diese Texturen die sRGB-Decodierung. Vier headless
DDS-Regressionstests decken Mipfaces, abgeschnittene Nutzdaten und nicht
unterstützte DX10-Formate ab. Die Cubemap-Generatoroption `--hdr-cubes` erzeugt
Werte oberhalb 1 ohne LDR-Clipping.
Lancer und LancerEdit bauen mit diesem Pfad. Ein 256-Pixel-HDR-Cube belegt mit
Mips ungefähr 4,2 MB statt rund 2,1 MB für RGBA8; das Budget für gleichzeitig
resident gehaltene Cubes muss noch gemessen und begrenzt werden.

Der Surface-NAP wurde ausschließlich aus `Data/NAP/Surface/` gepackt und mit
45 Einträgen verifiziert. Ein isolierter NAP-only-LancerEdit-Lauf lud die
Freelancer-Daten und zeigte LI_ELITE im ModelViewer. Die frühere Auswahl von
`envcube_sun` im `Textured`-Modus lud erwartungsgemäß keinen Cube; der Render-Tab
führt nun direkt in den Lit-Modus und unterscheidet „noch nicht geladen“ von
einem Cube-Load-Fehler. Am 2026-10-18 bestätigte ein NAP-only-Lit-Lauf den
`envcube_sun`-Load als `TextureCube` und einen Ein/Aus-Bildunterschied auf dem
LI-ELITE-Draw. Der DDS ist DX10 RGBA16F und enthält RGB-Werte über 1. Messwerte
und Aufnahmen stehen unter
[`HDR cubemap live A/B`](evidence/renderer/hdr-cubemap-live-ab-20261018/README.md).
Die Änderung bleibt subtil; Retail-Orientierung, Materialkalibrierung und
weitere feste Ingame-Szenen sind noch abzunehmen.

## Normalmap-Qualität: BW Pygar airlock (2026-10-18)

Ein kontrollierter LancerEdit-Lit-A/B des NAP-Modells `bw_pygar_airlock.cmp`
trennte die drei sichtbaren Normalmap-Zuordnungen einzeln. Die stärkste
Streifenwirkung kam von `dig_site_18.tga` bei der ursprünglichen Stärke 1,09;
`dig_site_20.tga` bei 4,48 wirkte ebenfalls sichtbar. `dig_site_16.tga` bei
0,73 änderte in dieser Ansicht keine Pixel. Roughness-/Metallic-Maps und
PBR-Reflexionen waren aus, Modell, Kamera und Licht blieben konstant.

Der gemeinsam getestete Satz 0,73 / 0,50 / 2,00 für die drei Karten behielt
Oberflächendetails und senkte das Ein/Aus-RMSE im Viewport-Crop von 0,0441831
auf 0,0242191. Diese Stärken wurden in `Data/SDK/fx/SurfaceMaps.ini`,
`Data/NAP/Surface/fx/SurfaceMaps.ini` und den Generatorparametern übernommen.
Die Karten sind aus Diffuse-Helligkeit abgeleitete Schätzungen; die Änderung
ist eine visuelle Kalibrierung dieses Modells, kein allgemeiner Tangentenfix.
Weitere Schiffe, Basen und Spielansichten müssen noch abgenommen werden.
Messwerte, Manifest-Provenienz und Bilder: [`BW Pygar airlock Normalmap A/B`](evidence/renderer/bw-pygar-map-isolation-ab-20261018/README.md).

## Minimize/Restore und Framebuffer-Resize (2026-10-18)

Ein LancerEdit-Lauf auf SDL3/OpenGL mit Mesa llvmpipe lädt die NAP-Daten bis
`Finished loading game data`. Im reproduzierten XWayland-Test löste
`xdotool windowclose` die Ereignisse `MINIMIZED`, `OCCLUDED`, `FOCUS_LOST`,
`RESTORED` und `EXPOSED` aus; danach meldete Mesa `GL_OUT_OF_MEMORY in Resizing
framebuffer`. Es gab dabei kein `CLOSE_REQUESTED`- oder `DESTROYED`-Event für
das Hauptfenster. Deshalb ist dieser Lauf kein Beleg für einen Fehler im
normalen SDL-Fenster-Schließpfad. Ein separates, kurzlebiges SDL-Fenster wird
beim Start zerstört; der Eventhandler filtert `WINDOW_DESTROYED` nun nach der
Hauptfenster-ID, damit dieses Nebenfenster den Renderer nicht beendet.

Beim Ersetzen eines AA/HDR/SSAO-Ziels entsorgt der Renderer jetzt zuerst den
bisherigen vollständigen Zielsatz und legt danach den neuen an. Damit entfällt
die gleichzeitige GPU-Residency beider Full-Resolution-Zielsätze während Resize
und Optionswechsel. Ein neuer NAP-only-Release-Lauf mit HDR, Bloom, SSAO und
SMAA High durchlief 960×540, 1280×720, 1920×995 sowie Minimize/Restore auf
Intel HD 4600 ohne `GL_OUT_OF_MEMORY` oder Rendererfehler. Das prüft reguläre
Größenwechsel, reproduziert aber nicht den älteren `xdotool windowclose`-Ablauf;
dessen OCCLUDED-/FOCUS_LOST-Ereignisse und Hardware-GL bleiben offen. Siehe
[`Resize target reallocation runtime`](evidence/renderer/render-target-resize-release-20261018/README.md).
NAP-Paket und Log des bisherigen Laufs liegen in
`/tmp/lancer-renderer-lit-20261018/` und sind nur lokale Diagnoseartefakte.

## OpenGL shadow depth-range runtime check (2026-10-18)

Nach Patch 2374 wurde der aktuelle Release-Client in einem NAP-only-OpenGL-Lauf
auf Intel HD Graphics 4600 gestartet. Die FP7-Intro-Szene erreichte den
Systemrenderer; das Debug-Overlay meldete einen aktiven Shadow-Map-Pass mit
`0,210 ms`. Damit ist die OpenGL-Ausführung nach dem expliziten
Clip-Tiefenvertrag bestätigt. Der Lauf ist keine feste Vorher/Nachher-Aufnahme
und bewertet weder Schattenqualität noch Lichtauswahl oder FPS. Manifest,
Archivprüfungen, Logauszug und Screenshot stehen unter
[`OpenGL shadow depth-range runtime check`](evidence/renderer/opengl-shadow-depth-contract-20261018/README.md).

## Offene Renderer-Arbeiten

1. **Farbvertrag abschließen.** Legacy-Albedo, diffuse Materialfarben, Vertexfarben,
   Emission, Fog, Partikel, transparente Effekte und Clear-Farben je Pfad
   eindeutig als sRGB-Eingabe oder lineare Größe behandeln. Beleuchtung und
   Blending im HDR-Ziel müssen konsistent linear erfolgen; LDR muss unverändert
   als Rückfallprofil erhalten bleiben.
2. **PBR/IBL visuell abnehmen.** GGX-Prefilterung und diffuse Irradiance sind
   in Generator, NAP-Assets, Metadaten und Shaderpfad vorhanden. Cubemap-
   Orientierung, Roughness-Verlauf, Energieerhaltung, Normalstärke und fehlende
   Texturen auf Schiff, Basis, Planet und DFM mit festen Kameras vergleichen.
   Ein neuer NAP-only ModelViewer-A/B zeigt sichtbare Änderungen durch Surface-
   PBR-Maps und Normalmaps; beim neutralgrauen `envmapbasic`-Defaultcube bleiben
   Umweltreflexionen erwartungsgemäß pixelidentisch. Die Messreihe und ihre
   Grenzen stehen unter
   [`LI Elite PBR live A/B`](evidence/renderer/pbr-live-ab-nap-20261017/README.md).
   Ein Same-Session-A/B mit dem nichtgrauen HDR-`envcube_sun` bestätigt, dass
   Cube-Load und Reflexionsschalter den LI-ELITE-Produktionsdraw beeinflussen;
   die mittlere Bildabweichung bleibt gering. Reflexionsstärke,
   Materialkalibrierung und feste Ingame-Szenen sind deshalb weiterhin offen.
   Messwerte stehen in den Berichten zu [LI Elite PBR](evidence/renderer/pbr-live-ab-nap-20261017/README.md)
   und [HDR cubemap live A/B](evidence/renderer/hdr-cubemap-live-ab-20261018/README.md).
   Ein NAP-only ModelViewer-Lauf prüft jetzt auch den DFM-Materialpfad: das
   Aktivieren der normalen Surface-Map ändert 4,02 % der modellnahen Crop-Pixel;
   Roughness-/Metallic-Maps ändern danach weitere 6,79 %. Die QA-Zuordnung nutzt
   mangels Charakterkarten eine LI-Schiffshüllen-Map auf dem Anzug und belegt
   daher nur das Laden und die Wirkung der DFM-Maps, nicht deren Qualität.
   Charaktergerechte Karten und feste Ingame-DFM-Szenen bleiben offen. Siehe
   [DFM Surface-map runtime A/B](evidence/renderer/dfm-surface-map-nap-20261019/README.md).
3. **Schattenqualität abnehmen.** Der dominante Richtungslichtpfad einschließlich
   Shadow-Target, Frustum-Culling und 3×3-PCF ist integriert. `render_shadow_bias`
   (0 bis 0,02; Standard 0,0015) ist jetzt als INI-Wert und
   `--render-shadow-bias`-Startparameter einstellbar. INI-Serialisierung und
   Copy/Apply erhalten den Wert; die Laufzeitvalidierung weist Werte außerhalb
   dieses Bereichs zurück. Das ermöglicht reproduzierbare Kalibrierungen, ersetzt
   aber keine Szenenabnahme. Ein erster NAP-only-Hi02-Vergleich auf Intel HD 4600
   bestätigt den aktiven Schattenpass bei Bias 0 und 0,02; die Aufnahmen und
   Messgrenzen stehen unter [Hi02 Shadow-Bias A/B](evidence/renderer/shadow-camera-hi02-bias-20261018/README.md).
   Bias-Kalibrierung, Caster-Abdeckung,
   Alphamaterialien, Detailflimmern und GPU-Kosten in festen Schiffs-/Basisszenen
   vergleichen; Cascades und weitere Schattenlichter bleiben Erweiterungen.
   Die vorhandene Hi02-Leistungsmessung auf Intel HD 4600 erreichte bei
   1280×720 einen Median von 99,44 FPS mit aktivierten Renderoptionen;
   der Debug-Profiler war dabei eingeschaltet. Sie ist Diagnosematerial,
   keine Release-Baseline. Für die kommende RTX-5060-Umgebung gelten die
   kontrollierten Profile und Messgrenzen aus [Performance-Abnahmematrix]
   (evidence/renderer/performance-acceptance-matrix-20261018.md).
4. **Atmosphären und Effekte vervollständigen.** `AtmosphereMaterial.Fade` wird
   vom MAT-Parser bis zum Shader-Parameter weitergereicht, im Shader jedoch
   nicht verwendet. Retail-MATs setzen den Wert typischerweise auf 1,1; ein
   Retail-MAT und Binärspur sind unter
   `docs/evidence/renderer/atmosphere-fade-retail-20261017.md` dokumentiert;
   `Fade=1.1` ist bestätigt, seine Retail-Berechnung bleibt ungeklärt. Die
   Ghidra-Nachprüfung ordnet die gefundenen `Fade`-Strings nur dem separaten
   `Fade 1D Texture`-Sampler zu; sie belegt keine Verwendung des MAT-Skalars.
   Vor einer Änderung muss die tatsächliche Winkelwirkung im Retailpfad geklärt
   werden.
   Der Soft-Particle-Pfad bindet eine samplebare Szenentiefen-Textur in Partikel-
   und Beamshader ein und bietet einen Legacy-Fallback bei fehlgeschlagener
   Zielallokation. Vor dem Abtasten wird bei einem Backbuffer-Viewport dessen
   OpenGL-Ursprung unten links umgerechnet und von den Fragmentkoordinaten
   abgezogen; dadurch passt Letterboxing zum lokalen Offscreen-Tiefenziel. Ein
   NAP-only-ON/OFF-Lauf mit den Modi `--render-soft-particles=on|off` zeigt in
   der M01A-Basenexplosion eine sichtbare Änderung der Partikelwolke und ihrer
   Überlagerung mit der Basis. Die Aufnahmen folgen demselben Missions-Trigger
   in getrennten Runs; Kamera und Partikel bewegen sich weiter und erlauben daher
   keine pixelgenaue Tiefen-Fade-Kalibrierung. Tiefenkanten, Letterbox-Viewports,
   transparente Überlagerungen und GPU-Kosten müssen mit fixierter Kamera und
   in weiteren NAP-Szenen geprüft werden. Siehe
   [`Soft-particle M01A A/B`](evidence/renderer/soft-particle-m01a-explosion-ab-20261018/README.md).
   Sonnen-/Planetenatmosphären, Lens Glow, Jump-Gates,
   Tradelanes, Laser, Explosionen und Partikel ebenfalls in repräsentativen
   Szenen abnehmen.
5. **Render-Graph backendneutralisieren.** Der Systemrenderer und Postprocessing
   führen ihre Pässe bereits über den Graph-Scheduler aus und validieren
   Attachment-Verträge. Die Attachment-Formate in den Contracts tragen nun keine
   GL-Backendnamen mehr; die konkreten Speicherformate werden weiterhin vom
   Backend angefordert. Der aktuelle Systemrenderer enthält keine direkten
   GL-Aufrufe; seine Pass-Callbacks setzen jedoch sofort Zustand und Ressourcen
   über den konkreten `RenderContext`, dessen Laufzeitfabrik weiterhin das
   OpenGL-Backend erzeugt. Der Pass-Graph plant und validiert Callbacks, zeichnet
   aber keine backendneutralen GPU-Kommandos auf und verwaltet kein Aliasing.
   SDL_GPU besitzt weiterhin nur einen separaten Diagnosepfad. Eine echte
   Backend-Ausführung, Ressourcen-Lebenszeiten und Fehlerwiederherstellung
   bleiben offen. Patch 2370 passt die PBR-Fragmentuniforms an SDL GPUs
   Vier-Block-Limit an: Der View-Vektor kommt aus dem Vertexshader und die
   UV-Selektoren liegen im PBR-Parameterblock. Der aktive PBR-Pfad benötigt so
   drei Fragmentuniformblöcke ohne Schatten und vier mit Schatten. Das beseitigt
   nur diese Shader-Ressourcengrenze; Spielshader-Bindung und Renderer-Ausführung
   sind weiterhin offen.
6. **Vulkan/SDL_GPU-Spike und Backend.** Der Produktionsshader-Audit erstellt
   und gibt alle Varianten aus 25 Shader-Bundles frei. Für `Basic_FVF` und
   `PBR` erhält er die Produktionsdeklaration von `FVFVertex`, für
   `Basic_Skinned` und `PBR_Skinned` jene von `DfmTangentVertex`. Der
   Produktions-PBR-Draw-Smoke nutzt ebenfalls das aus der echten
   `FVFVertex`-Deklaration abgeleitete GPU-Layout. Als nächstes echte VMesh- und
   DFM-Assets samt Materialbindung laden und zeichnen. Patch 2398 lädt jetzt
   einen vollständigen indizierten VMesh-Materialdraw, seine MAT-Diffuse-Textur
   und Surface-Normal/Roughness/Metallic aus dem aktiven NAP-Provider. Der
   `br_elite`-Lauf auf SDL_GPU/Vulkan verwendete 1.299 Draw-Indizes und änderte
   bei Surface-Maps 43.794 Pixel gegenüber der Off-Aufnahme. Die Aufnahme zeigt
   Diagnose- und Schattenpanels und ist kein Qualitätsnachweis für
   Schiffsoberflächen. Der Test nutzt nur einen Materialdraw und weiterhin
   synthetische Szenegeometrie. Siehe
   [NAP SDL_GPU Bretonia Elite A/B](evidence/renderer/br-elite-sdl-gpu-nap-20261019/README.md).
   Patch 2399 assembliert den CMP-Construct-Baum und wendet den authored
   Teiltransform auf den ausgewählten Diagnose-VMesh an. Das bleibt ein Draw und
   ist noch kein vollständiger Schiffsnachweis. CMP-Teiltransforms für alle
   Komponenten, sämtliche Schiffsmaterialien, BC4/BC5, Cubemaps, Skinning,
   Renderziele und Präsentation müssen in realen Szenen zusammen geprüft werden;
   Patch 2399 braucht noch einen NAP-Lauf mit einem Teil, dessen Construct einen
   sichtbaren Nicht-Identitätstransform hat.
   Erst nach erfolgreichem vollständigem Spike den gesamten
   Spiel- und Editorpfad portieren; OpenGL bleibt währenddessen die Vergleichs-
   und Rückfallreferenz. Der Audit auf Mesa/Vulkan Haswell ist wegen der
   eingeschränkten Treiberunterstützung kein GPU-Kompatibilitätsnachweis.
   Details:
   [SDL_GPU FVF/DFM-Pipeline-Audit](evidence/renderer/sdl-gpu-real-fvf-input-pipeline-20261019.md).
7. **Stabilität und Performance.** Feste Szenen und Kameras für Intro,
   Kampf, Nebel, Asteroidenfeld, Basis, Planet und DFM erfassen. CPU/GPU-Zeit,
   Drawcalls, Uploads, Speicher und Pipeline-Kompilierung mit identischen
   Optionen vergleichen. Der CLI-Modus `--render-profile-capture=on` exportiert
   dafür 300 Frames ohne sichtbares Debug-Overlay und führt die am ersten
   Profilframe aktiven Renderoptionen als JSON in jeder CSV-Zeile mit. Live-
   Änderungen während einer Aufnahme müssen weiterhin getrennt protokolliert
   werden. Ein aktueller NAP-only-Release-Lauf mit allen Renderoptionen an
   bestätigte den CLI-Export und lieferte 300 CPU-/GPU-Samples samt Optionssnapshot
   (FPS-Median 89,13; 32 verspätete Simulationstakte beim Introübergang). Auch
   dieser einzelne bewegte M01A-Lauf ist nur ein Diagnoseprofil, keine stabile
   Baseline. Siehe
   [Release-Profil mit aktiven Renderoptionen](evidence/renderer/profile-capture-release-smoke-20261018/README.md).
   Resize, Minimize/Restore, Kontextverlust, Shaderfehler
   und Ressourcenfreigabe wiederholt prüfen.
8. **Release-Abnahme.** Linux und Windows auf dokumentierten GPUs/Treibern
   vergleichen, Profile/Optionen beim Neustart und live prüfen, Fallbacks für
   fehlende Fähigkeiten validieren und NAP-only-Testpakete auf Herkunft,
   Manifest und Hashes prüfen.

## Reihenfolge und Abschlusskriterien

| Schritt | Arbeit | Fertig, wenn |
|---|---|---|
| A | Farbvertrag und HDR-Eingänge | Alle Szenen-Shader verwenden dieselben dokumentierten Farbregeln; Material-, Alpha- und Clear-Fixtures bestehen und HDR/LDR-Referenzbilder sind vergleichbar. |
| B | IBL und Materialqualität | Metall, Lack und nichtmetallische Flächen auf Schiff, Basis, Planet und DFM visuell geprüft sind; Cubemap-Richtung, Roughness und GPU-Kosten dokumentiert sind. |
| C | Schatten und dynamische Effekte | Beleuchtungs- und Effektpfade eine kontrollierte Retail-Szenenmatrix bestehen und die Kostenbudgets bekannt sind. |
| D | Render-Graph und Lifecycle | Passverträge mit realen Backend-Operationen übereinstimmen; `SystemRenderPassExecution.End` weist nicht veröffentlichte Pflichtausgaben zurück und lässt optionale Attachments aus; Resize, Fehler und Wiederherstellung hinterlassen keine Ressourcen- oder Zustandslecks. |
| E | Vulkan/SDL_GPU | Der vollständige Client- und Editorpfad läuft mit denselben Kernmaterialien; GL/Vulkan-Bild- und Lifecycle-Vergleich ist dokumentiert. |
| F | Stabilität/Performance/Release | Linux-/Windows-Matrix, langfristige Profile, Fehlerpfade und NAP-only-Paketprüfung dokumentiert und reproduzierbar sind. |

Die fachliche Reihenfolge entspricht dem ausführlicheren
[Grafik-Umsetzungsplan](graphics-implementation-plan.md). Änderungen am Client
bleiben patch-only in `patches/`; nach jeder Änderung muss eine vollständige
Overlay-Rekonstruktion erfolgen. NAP-Container für Client-/Editor-Laufzeitprüfungen
werden ausschließlich aus Dateien unter `Data/NAP/` gebaut.

Patch `2340-backend-neutral-depth-function.patch` and `2341-neutralize-wrap-and-vertex-enums.patch` also passed a Release NAP-only startup smoke on OpenGL 4.5 / Mesa llvmpipe: the application loaded game data, initialized the interface, and rendered the main-menu scene. The 13 archive hashes and archive contents were checked against the active manifest; no loose `DATA` directory was present. Capture and runtime boundaries: [`backend enum NAP smoke`](evidence/renderer/neutral-enums-nap-smoke-20261018/README.md). This startup image does not replace a fixed-scene before/after GL capture.

## Aktueller Renderer-Fix (2026-10-18)

`DepthFunction` wurde im OpenGL-Backend bisher nur bei der Kontextinitialisierung
gesetzt. Spätere Anforderungen für `Less` und `LessEqual` erreichten deshalb den
GPU-Zustand nicht. Patch `2340-backend-neutral-depth-function.patch` ordnet die
Vergleiche backendneutralen Enum-Werten zu, mappt sie im OpenGL-Backend und
wendet Änderungen bei `ApplyState` an. Ergänzend löst er Vertexformate und
Textur-Wrap-Modi von GL-Konstanten und mappt diese erst im GL-Backend. Dreizehn
Regressionstests prüfen die Mappings und ungültige Enumwerte. Eine GL-Frame-
Capture-Abnahme folgt mit der Renderer-Szenenmatrix.

Der HDR-NAP-Lauf zeigte einen doppelten Letterbox-Versatz: Wenn SSAO in ein
Zwischenziel schreibt, muss dessen Viewport bei `(0,0)` beginnen. Der Aufrufer
übergibt für die THN-Szene einen Drawable-Offset von `(0,101)` bei einer
Zielgröße von `1280x517`; dieser Offset wurde fälschlich auch auf das lokale
SSAO-Ziel angewendet. Patch `2333-ssao-offscreen-local-viewport.patch` setzt den
offset auf Null für Zwischenziele und behält ihn für den direkten finalen
Drawable-Pass.

Der Client-Build und beide Offset-Regressionstests waren erfolgreich (0 Fehler;
der Build meldet bestehende Compilerwarnungen). Der erneute NAP-only-HDR-Lauf
wurde mit 13 frisch aus `Data/NAP/` gebauten und vollständig verifizierten
Archiven ausgeführt; das App-Verzeichnis enthielt kein loses `DATA` oder `data`.
Die Laufzeit erreichte `Finished loading game data`, `Interface loaded` und
`Entering system FP7_system`. Der Bildvergleich misst vor dem Fix sichtbare
Bildzeilen `202–516` und danach `101–617`; damit stimmen die Bildgrenzen mit dem
absichtlichen Letterbox-Viewport überein. Die Aufnahmen und Manifest-Provenienz
liegen unter [`SSAO intermediate viewport HDR regression`](evidence/renderer/ssao-intermediate-viewport-hdr-20261018/README.md).
Der Nachweis gilt für OpenGL/Mesa llvmpipe und ersetzt keine Abnahme auf weiteren
Treibern oder GPUs.

Patch `2345-opengl-texture-region-readback.patch` vervollständigt außerdem den
synchronen `Texture2D.GetData`-Vertrag: Mip-Level, optionale Rechtecke,
Zieloffset und Elementanzahl werden validiert und angewendet. Desktop-GL liest
die ausgewählte Mip-Ebene direkt mit `glGetTexImage`; GLES nutzt ein temporäres
FBO, stellt getrennte Read-/Draw-Framebufferbindungen, Pixel-Pack-Alignment
und PBO-Bindung wieder her und liest BGRA8 über RGBA mit nachfolgendem
Kanalaustausch. Komprimierte Formate werden gezielt abgewiesen, da dieser Pfad
sie nicht dekomprimiert. Patch `2346-gl-texture-readback-smoke.patch` ergänzt
einen separaten Runtime-Probe ohne Freelancer-Daten; er besteht auf Mesa/OpenGL
mit Intel HD 4600 für Mip 1, Teilrechteck, Offset/Count, Pixelwerte und
ungültige Argumente. Editor-Export und GLES-Abnahme bleiben offen.

Patch `2348-offset-soft-particle-viewport-uvs.patch` zieht bei der
Soft-Particle-Tiefenabtastung den tatsächlichen OpenGL-Viewportursprung ab.
`CurrentViewport.Y` ist oben-links-basiert; für den Backbuffer wird es anhand
der Drawable-Höhe in den unteren GL-Ursprung umgerechnet. Für lokale
Offscreen-Ziele bleibt der lokale Viewportursprung maßgeblich. Der GLSL-Dump
bestätigt, dass `SV_Position` als `gl_FragCoord` übersetzt wird. Overlay-Aufbau
bestand mit 965 Dateien; der Release-Build bestand mit 0 Fehlern und 131
Warnungen. Mit dem SHA-256-geprüften 13er-NAP-Manifest
`lancer-softparticle-20261018` lud der Release-Client auf Intel HD 4600 die
Spiel-Daten, stellte den Hi02-Spielstand wieder her und betrat das System.
Eine Bildabnahme der Depth-Kanten und eine kontrollierte Soft-Particle-ON/OFF-
Messung sind weiterhin offen.

Patch `2358-guard-ambient-only-material-lighting.patch` behebt einen Crash beim
Zeichnen opaker Materialien mit aktivem Ambient-Licht und ausgeschalteter
Dynamikbeleuchtung: `Lighting` besitzt in diesem gültigen Pfad keine
`SourceLighting`-Referenz. `RenderMaterial.SetLights` überspringt deshalb die
Quelllichtschleife, wenn keine Quellbeleuchtung gesetzt ist. Ein NAP-only-Lauf
mit dynamischer Beleuchtung aus erreichte `FP7_system` und zeichnete die Szene
ohne diesen Crash.

Die Pause-Schaltflächen waren im ersten Lauf leer, obwohl die TTF-Dateien
vorhanden waren. Das Log meldete fehlende String-IDs und Resource-DLLs. Die DLLs
lagen im NAP unter `EXE/`, die erzeugte `librelancer.ini` forderte sie am
Paketstamm an. Patch `2359-librelancer-resource-dll-exe-fallback.patch` lässt
`FreelancerIni` bei dieser Konfiguration zusätzlich unter `EXE/` suchen. Der
NAP-only-Nachlauf zeigt alle fünf Pause-Beschriftungen; die vorher fehlenden
String-IDs und `Dll:`-Warnungen treten nicht mehr auf. Vorher-/Nachherbilder,
Manifest und Laufzeitgrenzen stehen unter
[`Resource-DLL UI smoke`](evidence/renderer/resource-dll-nap-smoke-20261018/README.md).

Patch `2361-render-effects-options-layout.patch` verkleinert die HDR-/Bloom-
Trefferflächen und ordnet alle Effektoptionen in den 450-Einheiten-Rahmen ein.
Der Release-NAP-Lauf zeigt alle Regler einschließlich des unteren Bloom-Kernels
innerhalb des Rahmens. Der Exposure-Thumb bewegte sich nach einem Scroll-Schritt
über dem Regler sichtbar, HDR blieb aktiv. Die Oberfläche zeigt keinen
numerischen EV-Wert; Bloom-Kalibrierung und GPU-Kosten bleiben offen. Die
Laufzeitbilder und Paketprüfungen stehen unter
[`Effects-Layout Live Smoke`](evidence/renderer/render-effects-options-layout-live-20261018/README.md).

Ein frischer Release-NAP-Lauf nach dem gemeldeten Absturz reproduzierte den
Ambient-only-Pfad mit ausgeschalteter Dynamikbeleuchtung und erreichte
`FP7_system`, ohne `RenderMaterial.SetLights`-Ausnahme. Derselbe Build zeigte
die Station bei aktiviertem Dynamiklicht beleuchtet; beim
Abschalten im Optionsmenü wurde sie nach der Rückkehr ins Spiel dunkel. Das
stützt die Diagnose, dass die frühere Intro-Dunkelheit mit deaktivierter
Dynamikbeleuchtung zusammenhing. Die Kamerapositionen der Startaufnahmen
weichen ab; sie sind keine pixelgenaue Lichtabnahme. NAP-Manifest, Build- und
Laufzeitnachweis sowie Bilder stehen unter
[`Intro lighting smoke`](evidence/renderer/dynamic-lighting-intro-smoke-20261018/README.md).


## SDL_GPU-Probe

`lancer --sdl-gpu-smoke` initialisiert SDL3, erstellt ein SDL_GPU-Gerät,
beansprucht ein Fenster ohne OpenGL-Flag und erwirbt ein Swapchain-Bild. Der
normale Shader-Build erzeugt ein SPIR-V-Vertex-/Fragmentpaar. Der Probe lädt
einen interleavten Vertexbuffer aus `FLOAT3`-Position mit Clip-Space-Tiefe und
`FLOAT2`-UV über
einen Transferbuffer hoch und richtet das Vertexlayout ein. Zusätzlich lädt er
einen bekannten 4×4-BC5-Block mit konstanten Normal-XY-Werten hoch, bindet
einen Nearest-Sampler und rekonstruiert die Z-Komponente im Fragmentshader.
Ein geprüftes D32-Depth-Target wird auf Swapchain-Größe angelegt und im Pass
mit `LESS` sowie Depth-Write verwendet. Ein zweiter Draw gleicher Tiefe bindet
eine andere BC5-Textur; die Pixelabnahme muss den ersten Draw behalten. Damit
weist derselbe Readback auch nach, dass Depth-Target, Depth-Test und Depth-Write
wirksam sind. Vor der Anlage wird die Formatfähigkeit des SDL_GPU-Geräts
abgefragt. Der Shadercompiler übergibt nun die aus SPIR-V
reflektierten Sampler-, Storage- und Uniformbuffer-Zahlen korrekt an SDL_GPU;
zuvor blieben diese Felder trotz vorhandener Reflektion auf null.

Nach dem Draw lädt ein Download-Transferbuffer den mittleren Swapchain-Pixel
zurück. Der Probe wartet auf das Fence und vergleicht den Pixel mit der
bekannten rekonstruierten Normalfarbe. Damit sind Shader-Pipeline, Vertexbuffer und -layout,
BC5-Textur-Upload, Samplerbindung, Fragment-Sampling, Normalrekonstruktion, Pixel-Readback und
Präsentation auf SDL_GPU praktisch geprüft. Der Probe enthält weiterhin keine
Freelancer-Spielshader, Skinning, Materialauflösung oder Spiel-/Editor-Szenen;
er ersetzt die SDL_GPU-Spike-Abnahme aus P5 nicht.

Am 18.10.2026 bestand der Release-Client den Probe auf Mesa/Vulkan mit Intel
Haswell sowie auf Lavapipe. Beide Läufe lieferten bei 640×360 den Mittelpunkt
`(204, 153, 226, 255)` innerhalb der UNORM-Toleranz. Mesa meldet
unvollständige Haswell-Vulkan-Unterstützung. Der konstante BC5-Block prüft den
komprimierten Formatpfad, aber keine Freelancer-Texturen, Tangentenbasis,
Freelancer-FVF-Layouts oder Bildqualität in einer Spielszene.

Der erweiterte Depth-Probe bestand am selben Tag erneut mit beiden Treibern.
Er prüft den Mittelpunkt nach zwei überlappenden, gleich tiefen Draws mit
unterschiedlichen BC5-Texturen; `(204, 153, 226, 255)` bestätigt, dass der
zweite Draw durch `LESS` abgewiesen und der erste Depth-Wert geschrieben wurde.
Der Release-Build bestand mit 0 Fehlern und 180 Warnungen. Das bleibt ein
SDL_GPU-API-Probe und kein Port des Freelancer-Rendergraphen.

Der folgende Probe-Schritt rendert den BC5-Draw in ein samplebares
`R16G16B16A16_FLOAT`-Farbziel. Der Fragmentshader schreibt RGB-Werte über 1;
ein zweiter Pass liest diese Werte zurück und wendet die ACES-Fitted-Kurve an,
bevor er in die SDR-Swapchain zeichnet. Das abschließende Pixel-Readback prüft
damit Ressourcenformat, Offscreen-Pass, Depth-Ziel, Textursampling, HDR-Werte
über 1, Tonemapping und Präsentation zusammen. Der Release-Smoke lief auf
Mesa/Vulkan Intel Haswell und Lavapipe bei 640×360; beide lieferten den für die
ACES-Abbildung erwarteten Mittelpunktpixel `(226, 214, 230, 255)` innerhalb
der Format-Toleranz.
Der Vertexbuffer verwendet jetzt den Freelancer-FVF3-Interleave
Position/Normal/UV (3/3/2 Floats); der Shader bildet daraus eine TBN-Basis und
wendet die BC5-Normalmap an. Der synthetische Meshdraw nutzt außerdem ein
16-Bit-Indexbuffer mit `first_index=1` und `vertex_offset=3`; davor liegen
absichtlich degenerierte Dummy-Vertices, sodass ein fehlender Base-Vertex-Offset
keine sichtbare Fläche erzeugt. Der 18.10.2026-Release-Lauf auf Mesa/Vulkan mit
Intel Haswell bestand mit Mittelpunktpixel `(226, 214, 230, 255)`. Das prüft den
Drawvertrag, aber keine echten VMesh-Assets, Produktionsshader, DFM-Skinning,
Materialauflösung oder Freelancer-Spiel-/Editor-Szenen. Haswell-Vulkan-
Unterstützung ist laut Mesa unvollständig. Befehls- und Buildnachweis:
[`SDL_GPU FVF3 normal-map smoke`](evidence/renderer/sdl-gpu-fvf3-normal-smoke-20261018.md).

Die Shader-Bundles enthalten jetzt für SPIR-V die kompaktierten SDL_GPU-
Bindungsnummern und zusätzlich die Zuordnung von den bisherigen Slots. Der
Compilerlauf über alle 41 in Projektdateien registrierten Shaderbeschreibungen
und ihre Varianten war erfolgreich. Im PBR-Bundle wurden 512 Permutationen
erzeugt; die SPIR-V-Bindungen und serialisierten Slotmaps stimmen überein.
Der Vulkan-Smoke liest das neue Format und besteht mit dem bekannten
RGBA16F-/Depth-/BC5-Readback. Er lädt außerdem die vom Client gewählte Variante
des eingebetteten Freelancer-PBR-Bundles, erstellt daraus eine SDL_GPU-Pipeline,
zeichnet ein FVF3-Dreieck und prüft einen ausgelesenen PBR-Pixel gegen die
erwartete lineare Materialfarbe. Der PBR-Pfad verwendet jetzt einen interpolierten
View-Vektor und bündelt UV-Selektoren im PBR-Parameterblock. Damit benötigen
seine Fragmentvarianten drei Uniformblöcke ohne Schatten und vier mit Schatten,
innerhalb des SDL_GPU-Limits. Das belegt weiterhin keine Ausführung der echten
Freelancer-Meshes oder -Materialdaten und keinen Renderer-Bindungspfad im
SDL_GPU-Spielrenderer. `SDL3GpuShaderProgramReader` now owns the versioned SPIR-V payload and slot-map reader used by the SDL_GPU probe; it checks descriptor remaps against compact SPIR-V bindings before shader creation. This gives a production backend the same validated shader input contract without claiming that the renderer is ported.

The new `--sdl-gpu-shader-bundle-audit` command read all embedded bundles in `AllShaders`: 25 bundles and 3,487 feature variants, all with version-2 slot maps and no legacy or empty entries. It validates payload parsing and slot-map consistency, not 3,487 device pipelines. The run is documented in [`SDL_GPU production shader bundle audit`](evidence/renderer/sdl-gpu-production-bundle-audit-20261018.md). A fresh Release run on Mesa/Vulkan Intel Haswell parsed both PBR variants and retained the expected BC5, PBR and shadow readback pixels; the run details are in [`SDL_GPU shader bundle reader smoke`](evidence/renderer/sdl-gpu-shader-reader-smoke-20261018.md). Details: [`SDL_GPU production PBR draw`](evidence/renderer/sdl-gpu-production-pbr-draw-20261018.md).

The `--sdl-gpu-shader-compile-audit` command now creates and releases both
shader stages for every nonempty production variant through SDL_GPU on the
active device. Mesa/Vulkan Intel Haswell accepted all 3,487 variants (6,974
shader stages). This confirms SPIR-V driver compilation and the declared
per-stage resource counts for the current embedded catalog. It does not create
graphics pipelines for every vertex layout or prove draw compatibility; those
contracts and the game renderer backend remain open. See
[`SDL_GPU production shader compile audit`](evidence/renderer/sdl-gpu-production-shader-compile-audit-20261018.md).

The `--sdl-gpu-production-pipeline-audit` command then created and released
3,487 SDL_GPU graphics pipelines across the same 25 production bundles. Vertex
attribute locations and 32-bit scalar/vector formats are reflected from SPIR-V;
the probe uses a synthetic packed input buffer description and an RGBA8 color
target. This confirms SDL_GPU pipeline creation for every current shader pair,
but does not draw with retail Freelancer vertex buffers, material textures,
depth targets or render-target formats. The separate PBR FVF3 smoke remains the
draw-level evidence. See
[`SDL_GPU production pipeline audit`](evidence/renderer/sdl-gpu-production-pipeline-audit-20261018.md).

Der 18.10.-Smoke lädt nun zusätzlich die Produktionspermutation
`PBR_VIEW_VECTOR | SHADOWMAP` (Featuremaske 768). Zwei Draws derselben
Permutation verwenden dieselbe synthetische Beleuchtung und Geometrie; nur der
Inhalt der samplebaren D32-Tiefenkarte wechselt zwischen 0,6 und 0,4 bei
Empfängertiefe 0,5. Der Shader berücksichtigt nun, ob das Backend OpenGLs
`[-1,1]`-Clipbereich oder Vulkan/SDL_GPU's `0..1`-Clipbereich verwendet. Der Pixel
wechselt von `(54, 33, 9, 255)` zu `(0, 0, 0, 255)`. Das weist nach, dass der
Shader auf Vulkan tatsächlich die Tiefenkarte für die Lichttransmission
samplet. Der Spielrenderer besitzt weiterhin seinen separaten OpenGL-
Schattenpass; eine SDL_GPU-Ausführung des Spiel-Rendergraphs, echte Assets,
Skinned Meshes und Szenen bleiben offen. Eine NAP-only-OpenGL-Abnahme des
unveränderten Depth-Mappings ist nach der gemeinsamen Shaderänderung noch
erforderlich. Details und Grenzen stehen im
genannten Produktions-PBR-Nachweis.

## Backend-Grenzen

Der Screenshotpfad ist von SDL2/SDL3 nach `RenderContext.ReadBackbuffer`
verlagert. Das Render-Backend besitzt damit Format und Zugriff des Backbuffers;
die Plattformebene ruft keine OpenGL-Read-Funktionen mehr auf. OpenGL liest
BGRA8 aus dem Default-Framebuffer. Read-Framebuffer, Default-Framebuffer-Lesequelle,
Pixel-Pack-Puffer und Pack-Alignment werden nach dem Zugriff wiederhergestellt;
dadurch bleibt der CPU-Zielpuffer unabhängig vom vorherigen GPU-Readbackzustand dicht gepackt. Das Null-Backend liefert für Headless-Aufrufe einen
definierten leeren Pixelbereich. Der Vertrag ist eine Grundlage für weitere
Backends, aber noch kein Vulkan-/SDL_GPU-Nachweis. Screenshot-Readback ist
synchron und sollte bei häufigen Aufnahmen später über einen asynchronen
Readback-Pfad ergänzt werden.


## Dynamische Asteroiden-Effekte

`particle_effect` aus `Asteroidarch.ini` wird nun geparst, über `Effects.ini`
aufgelöst und als `ParticleEffectRenderer` an dynamische Asteroiden angehängt.
Der Renderer verwendet die normale Renderoption für Partikeleffekte und
übernimmt die konfigurierte Effektlichtkurve. Parser-Regressionen decken die
Badlands-/Lava-Namen sowie das optionale Fehlen des Eintrags ab. Der fokussierte
Testlauf besteht (3/3), der Release-Client-Build besteht (0 Warnungen, 0 Fehler).
Ein NAP-only Release-Start lädt Daten und UI bis `FP7_system`; dabei tritt keine `particle_effect`-Parsewarnung mehr auf. FP7 zeigt keine dynamischen Asteroiden, daher steht der sichtbare Effekt-Laufzeittest noch aus. Details:
[`Dynamic asteroid effects`](evidence/renderer/dynamic-asteroid-effects-20261018/README.md).

## Vollständiger NAP-only-Spielstart mit Renderer-Optionen (2026-10-19)

Ein Release-Lauf mit 13 aus `Data/NAP/` gepackten und verifizierten NAPs lud
Spieldaten und UI, initialisierte 53 Systeme und betrat `FP7_system`; die
M01A-Intro-Trigger liefen weiter. HDR, Normalmaps, ergänzende PBR-Maps,
Umgebungs-Cubemaps, diffuse IBL, dynamische Lichter, Schatten, Bloom und Soft
Particles waren gleichzeitig eingeschaltet. Es gab keine Crashmeldung und
keine `RenderMaterial.SetLights`-Ausnahme. Das belegt den Start des kombinierten
Optionssatzes mit vollständigen Paketdaten, jedoch keine Einzelwirkung oder
visuelle Qualität der Effekte. Ein späterer Frame-Capture zeigt Station, Schiffe,
Nebel, Sterne und Explosion in der M01A-Intro-Szene; er ist kein Ein/Aus-A/B und
belegt keine FPS. NAP-Manifest, Paketverifikation, Bild und Laufzeitgrenzen stehen unter [`Vollständiger NAP-only-Renderer-Lauf`](evidence/renderer/nap-full-runtime-20261019/README.md).
