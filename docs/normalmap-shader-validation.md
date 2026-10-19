# BC5 und TBN: Shaderprüfung vom 13.10.2026

## Geänderter Pfad

`Shaders/includes/NormalMap.hlsl` wird direkt von Basic/PBR und dem FVF-Vertexshader
eingebunden. Stärke 0 liefert eine flache Normalenkarte, 1 ist der Standard und
16 die obere Grenze. Die CPU lehnt NaN, Infinity und Werte außerhalb des Bereichs
ab. BasicParameters sind 64 Bytes (Stärkeoffset 48), PBRParameters 48 Bytes
(Stärkeoffset 44). Sieben Unit-Tests bestätigen Werte und Layout; zusammen mit
den Surface-Tests sind 89 Tests erfolgreich.

Die vorhandene FVF-Normalmap-Variante hat weiterhin Bit 16, ihr Name lautet nun
wie in den #ifdef-Blöcken NORMALMAP. Die Basis benutzt normalisierte, orthogonale
Tangenten und signed Handedness. Der bisherige Basisaufbau/Multiply-Pfad war
inkonsistent; nun gilt `worldNormal = mul(tangentNormal, basis)` mit den Zeilen
Tangente, Bitangente, Normale. Welt-Tangenten werden über World transformiert,
Normale weiterhin über NormalMatrix.

## OpenGL-Readback

Auf Mesa Intel HD Graphics 4600 / OpenGL 4.6 wurde ein separates Testprogramm
mit dem bestehenden LLShaderCompiler erzeugt. Es bindet eine bytegleiche Kopie
des geänderten NormalMap.hlsl ein. Ein Dreieck füllt ein 1x1-RenderTarget2D;
Fragmentuniforms liefern SampleXY, Stärke, Normale, Tangente und Handedness.
Der Fragmentshader gibt `(normal * 0.5 + 0.5, 1)` aus. Der BGRA8-Pixel wird
über Texture2D.GetData zurückgelesen und mit der analytischen Normale verglichen
(maximale Vektordistanz 0,025 für die 8-Bit-Quantisierung).

| Fall | SampleXY / Stärke | Erwartete Weltnormale |
| --- | --- | --- |
| Flach | (0.5,0.5) / 1 | (0,0,1) |
| Außerhalb der Unit Disk | (1,1) / 1 | normalize(1,1,0) |
| Stärke null am Rand | (1,1) / 0 | (0,0,1) |
| Gedrehte Basis | (0.5,0.5) / 1, N=(1,0,0), T=(0,0,-1) | (1,0,0) |
| Gespiegelte UV-Basis | (0.5,1) / 1, Handedness=-1 | (0,-1,0) |
| Degenerierte Tangente | (0.5,0.5) / 1, T=(0,0,0) | (0,0,1) |
| Stärke zwei | (0.7,0.5) / 2 | normalize(0.8,0,sqrt(0.84)) |

Alle sieben Readbacks bestanden. Der Solution-Build mitsamt den betroffenen
Shaderpermutationen hat null Fehler; bestehende Warnungen bleiben vorhanden.
Der NAP-only-Client erreicht das Hauptmenü und bestätigt native Navigation durch
Anzeige/Effekte/Performance, Escape und reguläres Exit. Die früheren CPU-Cache-
Leases bleiben korrekt. Messwerte stehen im [Prüfprotokoll](evidence/normalmap-shader-20261013.json).

## Grenzen und offene Arbeit

Der Readback prüft die gemeinsame GPU-Mathematik mit uniform gelieferten
BC5-Samples, keine komprimierte GPU-Textur und keinen vollständigen beleuchteten
Retail-Materialpfad. Supplemental-Sampler, Upload/Residency/Fences, UV-Seams,
Tangenten für die übrigen Retail-FVFs/DFM, reflektierte World-Skalierung und die
Bild-/Performance-Szenenmatrix bleiben offen. Deshalb werden die vorbereiteten
Grafikschalter nicht freigeschaltet.

Beim Freigeben des separaten kurzlebigen ShaderBundle wurde ein NativeBuffer-
Finalizerhinweis aus GLShader beobachtet. Shader/IShader besitzen derzeit keine
Dispose-Schnittstelle für den Uniformpuffer. Produktionsbundles werden länger
gehalten; deren Freigabe am Context-Ende ist damit noch nicht nachgewiesen und
muss im GPU-Lifecycle-Paket umgesetzt werden. Der Testprozess beendete regulär.

### Nachprüfung der Shader-Freigabe

Der Folgeschritt [Shader-Lebensdauer](shader-lifecycle.md) ergänzt Dispose und
den renderthreadgebundenen Shutdown. Derselbe siebenfach GPU-Test mit explizit
freigegebenem Testbundle liefert erneut korrekte Pixel, glIsProgram=false und
eine unveränderte NativeBuffer-Bilanz. Nach dem normalen Client-Shutdown sind
beide Shaderzähler null; nach erzwungener GC erscheint kein GLShader-Hinweis.
Die oben beschriebene ursprüngliche Beobachtung bleibt historische Evidenz.
