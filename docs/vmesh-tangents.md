# Retail-VMesh-Tangenten: Prüfung vom 13.10.2026

## Datenpfad

`VMeshTangentBuilder.Build(data, meshIndex, baseVertex, uvChannel)` bereitet einen
Materialdraw vor. Es gilt:

```
sourceVertex = VMeshRef.StartVertex + TMeshHeader.StartVertex + localIndex
```

Der Aufrufer liefert den Referenzoffset. TriangleStart/NumRefVertices bestimmen
den Indexbereich. Das Ergebnis hat lokale Indizes 0..N-1, MaterialCrc und UV-
Kanal und kann mit BaseVertex=0 hochgeladen werden. Der Quellmesh wird niemals
umgeschrieben. NORMAL+TEX1 wird TEX3, NORMAL+TEX2 wird TEX4; die beiden angehängten
float2-Attribute enthalten Tangent.xyz und Handedness als float4. Farben und
beide tatsächlichen UV-Sets bleiben bytegleich. Aufrufe dürfen den Eingabemesh
während der Berechnung nicht verändern.

MikkTSpace wird aus dem bestehenden SimpleMesh-Projekt eingebunden. Es erhält
Position, normalisierte Normale und den ausgewählten realen UV-Kanal pro Ecke.
Das Ergebnis wird pro Ecke gespeichert, statt es für einen mehrfach benutzten
Index zu überschreiben. Gespiegelte UV-Seams bleiben somit getrennt. Die Sign-
Umkehr des SimpleMesh-Exportadapters wird für die kanonische Runtime-Basis
zurückgenommen. Tangenten werden zur Normalen orthogonalisiert; degenerierte
Daten besitzen eine endliche Ersatzbasis. Non-finite Attribute, ungültige
Indexbereiche und trunkierte Vertexbuffer werden abgewiesen.

Bestehende TEX3/TEX4 enthalten bereits Tangentendaten und werden von Supports
abgelehnt; deren Überschreiben ist nicht Teil dieser API. VMeshData.GetTexCoord
akzeptiert jetzt genau 0..TexCoords-1 und weist negative/obere Grenzwerte ab.

## Verifiziert

- 104 relevante Tests bestanden, davon zehn neue Tangentenprüfungen. NORMAL+
  TEX1/TEX2 mit/ohne DIFFUSE, UV-Auswahl, gespiegelte gemeinsam benutzte Vertices,
  beide Basevertex-Offsets, degenerierte UVs, Erhalt der Eingabepuffer und
  ungültige Eingaben/Grenzen sind abgedeckt.
- Vollständiger Solution-Build: null Fehler, 20 Warnungen. SimpleMesh wird als
  bestehendes ProjectReference lokal kopiert; der reguläre lancer-Host erzeugt
  absichtlich keine deps.json (`GenerateDependencyFile=false`).
- Der aktive NAP-Snapshot liefert li_fighter.cmp. Alle unterstützten Referenz-
  Draws seiner Modelle/LODs wurden berechnet: 45 Draws, 5.841 Ecken. Alle
  Tangenten sind endlich und normiert; Vertexbuffer-SHA256 bleibt unverändert.
- Ein bytegleicher Basic_FVF.vert.hlsl samt Includes wurde mit einem separaten
  Prüf-Fragment kompiliert. Die resultierenden FVF3/FVF4-Bytes und lokalen Indizes
  wurden in VertexBuffer/ElementBuffer auf der realen OpenGL-GPU hochgeladen.
  Acht BGRA8-Readbacks bestätigen X- und Y-Richtung, eine gespiegelte zweite UV
  und beide DIFFUSE-Varianten. Der Vertexshader überträgt dabei die tatsächliche
  TBN-Matrix an den Fragmentshader; maximaler Normalenfehler ist 0,025.
- Alle sieben früheren BC5-/Basis-Readbacks bleiben grün. Probe-Shader und
  Uniformpuffer werden korrekt freigegeben; nach normalem Client-Exit sind
  beide Shaderzähler null. Native Options/Escape/Exit funktionieren weiterhin.

Der erste separate Probehost hatte die neue SimpleMesh-DLL nicht in seiner
benutzerdefinierten deps.json referenziert und beendete mit FileNotFound.
Nach expliziter Testreferenz wurde der komplette Lauf erfolgreich wiederholt.
Dies ist kein verschleierter grüner Erstlauf. DLL und regulärer Clientbuild
waren bereits korrekt vorhanden. Originale Assets und NAPs wurden nicht geändert.

## Runtime-Anbindung und offene Abnahme

`VisualMesh` reicht bei aktivierter Normalmap- und Tangentenerzeugungsoption und passender Material-
Binding den VMesh-Draw an einen per-VMeshResource-Tangentcache weiter. Der
MikkTSpace-CPU-Build läuft asynchron. Sobald er bereit ist, erzeugt der
Renderthread einen separaten FVF3/FVF4-Vertex-/Index-Upload; bis dahin läuft das
Originalmaterial ohne Zusatz-Normalmap. Meshbereiche mit ergänzenden Karten
werden nicht zusammenoptimiert, damit Meshindex und VMeshRef-Offset eindeutig
bleiben. Die GPU-Ressourcen werden mit dem VMeshResource freigegeben. Der
GameResourceManager begrenzt die logische Vertex- plus Index-Payload auf 64 MiB.
Bei vollem Budget bleibt das Originalmesh aktiv; ein Upload wird höchstens einmal
pro Sekunde erneut versucht. Das Budget misst keine Treiberreservierungen.
Es gibt keine LRU-Eviction: Ressourcen bleiben bis zum Dispose des VMeshResource
gebucht. Die Option `surface_generated_tangents` ist separat gespeichert und lässt sich im
Performance-Menü umschalten; ausgeschaltet werden erzeugte VMesh-Tangenten für
neue Draws nicht verwendet und bereits residente Tangenten-GPU-Ressourcen werden
freigegeben. Die CPU-Builddaten bleiben für ein späteres Wiedereinschalten im
VMesh-Cache. Authored Tangenten bleiben davon unberührt.

Die Budgetintegration wurde mit dem `LibreLancer.csproj`-Build verifiziert: 21
Projekte, 0 Fehler, 180 Warnungen. Der Patchoverlay-Rekonstruktor bestätigte 440
Dateien. Es wurden keine budgetbezogenen Unit- oder Laufzeittests ausgeführt;
Budgetgrenze, Retry, `ClearMeshes`- und Einstellungswechsel-Freigabe sowie Shutdown-Fall bleiben
explizite Testpunkte. Der neue Tangenten-Draw
wurde noch nicht nativ gerendert. Retail-Referenzbilder, Cache-/Leistungsprüfung,
DFM-/MaterialAnim-Skalierungen bleiben offen. Negative World-Skalierungen
berücksichtigen nun das Determinantenvorzeichen in der TBN und kehren bei
einseitigen Draws die Cull-Fläche um. Die visuelle GPU-Abnahme bleibt offen. Die nachfolgende
DFM-Erweiterung ist ein eigener Implementierungsstand ohne Bildabnahme.

## DFM-Tangenten und Skinning (Implementierung 2026-10-13)

`DfmTangentBuilder` erzeugt je FaceGroup und ausgewähltem Normalmap-UV-Kanal
separate `DfmTangentVertex`-Dreieckslisten. Jeder Corner behält Position,
Normale, beide UV-Sets, Bone-IDs und Gewichte des originalen `DfmVertex`.
Triangle-Strips werden mit wechselndem Winding aufgelöst; wiederholte Indizes
für degenerate Connector-Dreiecke werden unter Erhalt der Strip-Parität
übersprungen. Nichtindizierte Draws vermeiden einen 16-Bit-Indexüberlauf durch
zusätzliche Seam-Vertices. MikkTSpace, Signkorrektur und endliche Ersatzbasis
entsprechen dem VMesh-Pfad. Ungültige Indizes, nichtendliche Basisdaten,
unvollständige Dreiecke und Gruppen über 262.144 Corner werden abgewiesen.

`DfmMesh` entscheidet erst beim Draw anhand des aufgelösten Materials über den
Bedarf. Damit ist die Materialbibliothek bereits registriert. Die CPU-Arbeit
läuft asynchron; bis zum fertigen Upload wird das unveränderte Originalmesh
gezeichnet. `NmFlags.SecondUV` bestimmt sowohl die Erzeugung als auch das
Sampling; eine angeforderte, aber nicht vorhandene zweite UV führt zum
Originalpfad. Überschriebene Materialien bekommen dieselbe Bedarfsprüfung.
Die CPU-Ergebnisse bleiben je Gruppe/UV erhalten, auch nach Abschalten.

Der ResourceManager lädt DFM-Tangenten auf dem Renderthread und rechnet ihre
76 Bytes pro Corner gegen dasselbe 64-MiB-Payloadbudget wie VMesh-Tangenten.
Bei Budgetmangel bleibt das Originalmesh aktiv, mit maximal einem Uploadversuch
pro Sekunde. Einstellungswechsel, `ClearMeshes` und Manager-Dispose geben die
DFM-Tangentbuffer frei; alte Cachehandles erkennen ihre Freigabe. Originale
DFM-Buffers und Assets werden nicht überschrieben. Das Budget umfasst keine
Treiberreservierungen; es gibt keine LRU-Verdrängung. CPU-Cache und laufende
Buildtasks besitzen noch kein gemeinsames Speicher-/Parallelitätsbudget.

`BasicMaterial` erkennt das Tangentlayout und schaltet `NORMALMAP` ausschließlich
für dieses Layout frei. MAT-`NmSampler` behält Vorrang; ergänzende Surface-Karten
benötigen weiterhin `surface_normal_maps`. `surface_generated_tangents` steuert
alle generierten DFM-Tangenten einschließlich des MAT-Pfads. Der bestehende
Basic-Skinned-Shader transformiert Normale und Tangente ohne Translation,
transformiert die Tangente weiter durch World und orthogonalisiert die TBN-
Basis. Der Shader enthält nun Normalmap- und Envmap-Varianten. Transparente
Drawcommands tragen Bone-Puffer und Offset selbst, damit sie bei späterer
Ausführung nicht den Bonezustand eines anderen Materials übernehmen.

DFM-Materialien mit PBR-Werten verwenden den neuen `PBR_Skinned`-Pfad, der
die vorhandenen Bones verarbeitet. Geskinte DFM-Oberflächen können damit auch
Roughness-/Metallic-PBR nutzen; die sichtbare In-Game-Materialabnahme bleibt
offen. Ein NAP-only-LancerEdit-Lauf zeigt den texturierten DFM-Körper und die
separaten Render-Schalter; das ist noch keine PBR-A/B-Abnahme. Screenshots und
Laufgrenzen stehen im [DFM-PBR-Viewerbericht](evidence/renderer/dfm-pbr-viewer-20261015/README.md). Ein Headless-Test prüft
die Shaderbundle-Auswahl für geskinte DFM-Vertices mit und ohne Tangenten; ein
GPU-Draw oder sichtbarer Materialeffekt ist damit nicht abgenommen.
Nichtuniforme/negative Bone- und World-Skalierung,
negative MaterialAnim-Skalierung und Skinning mit singulären Mischmatrizen
brauchen eigene Shaderarbeit und Abnahme. Die DFM-LOD-Auswahl bleibt wie zuvor
auf Level 0 beschränkt; andere LODs werden hier nicht aktiviert.


### Isolierter Skinned-GPU-Readback (2026-10-13)

Ein nativer OpenGL-Probehost verwendete `DfmTangentBuilder`, das echte
`DfmTangentVertex`-Layout (76 Bytes, Tangente bei Offset 60), den bytegleichen
`Basic_Skinned.vert.hlsl`-Vertexshader aus dem aktuellen Clientbuild, einen
Bone-SSBO, eine echte BC5-Textur mit verschiedenen X-/Y-Blöcken und einen
Diagnose-Fragmentshader, der die berechnete Normale ausgibt. Neun BGRA8-
Readbacks prüften UV0, gespiegeltes UV1, Normalstärke 0, Translation, Bone-
Rotation Y/Z, gemischte Bonegewichte, Bonebuffer-Offset 256 sowie Bone- plus
World-Rotation. Alle neun lagen unter der Toleranz 0,025; der Maximalfehler
betrug 0,005641. Der Kontext wurde regulär beendet (`pending=0`,
`ownedShaders=0`, `totalShaders=0`). GPU und Details stehen in
[DFM-GPU-Nachweis](evidence/dfm-skinned-gpu-readback-20261013.json).

Der ursprüngliche Probehost liegt nur unter `/tmp` und ist kein dauerhaft
gepflegter Regressionstest. Sein Diagnose-Fragmentshader umgeht `BasicMaterial`
und `CommandBuffer`.

### Produktionsmaterial- und CommandBuffer-Readbacks (2026-10-13)

Eine getrennte Erweiterung verwendete die eingebetteten Produktionsshader
`Basic_Skinned`/`Basic_FVF`, `BasicMaterial` mit authored `NmSampler` und den
echten `CommandBuffer`. Sie prüfte sieben direkte Materialfälle (Stärke 0/1/2,
UV0/UV1, Bone-Rotation, Fallback ohne Normalmap und unskinned Tangentlayout),
zwei opaque Draws sowie zwei deferred Alpha-Composites in beiden
Submission-Reihenfolgen. Zwei Readbacks vor dem Flush bestätigten, dass deferred
Draws noch nicht ausgeführt wurden. Alle 13 Material-Readbacks bestanden;
maximaler RGB-Fehler 0,002811 bei Toleranz 0,015. Dazu bestehen weiterhin die
neun separaten Skinned-TBN-Readbacks mit maximalem Normalenfehler 0,005641 bei
Toleranz 0,025. Der Probeengine-Hash entspricht dem Build; Ressourcen wurden
ohne ausstehende GPU-Freigaben beendet. Der strukturierte Nachweis liegt unter
[DFM-Material/CommandBuffer](evidence/dfm-material-commandbuffer-gpu-readback-20261013.json).

Diese Materialfixtures verwenden synthetische DFM-Geometrie, manuell
bereitgestellte Bone-Matrizen und synthetische Texturen. Sie rufen nicht den
`DfmMesh`-Async-Generierungs-/Uploadcache auf, lösen keine echte NAP-
Materialmetadatenbindung auf und führen keinen Animationscontroller aus.
Supplemental `SurfaceGpuTextureCache`-Leases und deren Wiederherstellung nach
`AfterDraw` sind ebenfalls nicht enthalten. Der Nachweis ist kein
Retail-Szenen- oder NAP-Readback.

### Nachweise und verbleibende Abnahme

`DOTNET_ROOT=/opt/dotnet-10 dotnet build src/LibreLancer/LibreLancer.csproj
-p:UseSharedCompilation=false --no-restore` war erfolgreich: 0 Fehler,
129 Warnungen, 22 Projekt-Outputzeilen, 30 Sekunden. Die geänderten Skinned-
Shader wurden kompiliert (`Basic_Skinned.bin`, 183.428 Bytes). Der erste Versuch
ohne `DOTNET_ROOT` endete bereits beim Shadercompiler-Apphost mit fehlender
Runtime; nach Setzen des lokalen SDK-Pfads lief derselbe Build erfolgreich.
Die vollständige Patchrekonstruktion bestätigt 446 Overlaydateien; die Serie
bleibt bei 13 bestehenden Bündeln (Änderungen in 2003/2009/2010). Sieben
synthetische CPU-Tests für DFM-FaceGroups und Tangenten bestehen. Ein CPU-Lauf
über 204 NAP-DFM-Dateien (766 FaceGroups, 1.203 Varianten, 805.491 Corner)
baute alle Tangentenvarianten ohne Oversize-Fälle oder Fehler. Ein echter
Retail-DFM wird inzwischen über `DfmMesh` mit einer temporären NAP-QA-
Materialauflösung gerendert (siehe unten). Vollständige Retail-Materialauflösung
und passende Referenzbilder bleiben offen. Der synthetische `CommandBuffer`-Probe
deckt transparente Fixtures ab, aber nicht reale DFM-Teile in einer Szene.
Budgetgrenze, Abschalten/Wiedereinschalten und Shutdown brauchen noch
integrierte Cache-Läufe. Beide GPU-Probes ersetzen diese Abnahmen nicht.

**DFM-Normalmap-QA im LancerEdit-Viewer (2026-10-13):** Ein temporäres
`dfm-uv1-qa.nap` ordnet die drei Diffuse-Texturen aus
`li_male_elite_body.dfm` der vorhandenen Schiffskarte `nm_li_elite_hull` zu
(UV0, Stärke 0,65). Die direkte NAP-Auflösung fand alle drei Mappings, die
Bibliothek und keine Parserdiagnose. Im NAP-basierten LancerEdit-Viewer wurde
der echte DFM bei fester Kamera und aktiviertem Generate-missing-tangents im
Lit-Modus mit Surface-Map Preview an/aus aufgenommen. Die Karte erzeugt
sichtbare feine Oberflächendetails; ohne sie verschwinden diese. Ein Pillow-
Vergleich im Modellbereich maß RMSE 2,96/255, 21.824 Pixel mit einer
Kanalabweichung über 3 und maximal 91. Das belegt den DFM-Surface-Tangent- und
Shaderpfad für UV0. Die geliehene Schiffskarte ist keine passende
Charaktertextur; UV-Ausrichtung, Bildqualität und Laufzeitkosten bleiben offen.
Dieser DFM enthält kein UV1. Die Screenshots und A/B-Grafiken liegen unter
[`docs/evidence/renderer/`](evidence/renderer/); Prüfsummen und Kennzahlen
stehen in [`dfm-normalmaps-ab-20261013.json`](evidence/renderer/dfm-normalmaps-ab-20261013.json).

**UV1-Retail-DFM-Abnahme (2026-10-13):** Der Bestand enthält außerdem
`ku_kym_body_gen.dfm` und `worm.dfm` mit echtem UV1 samt `uv1_indices`.
`ku_kym_body_gen.dfm` hat sechs LODs; für LOD0–5 sind jeweils 528 UV1-Werte
und 2514/2073/1611/1084/546/274 UV1-Indizes vorhanden. Eine temporäre QA-
NAP-Zuordnung verknüpft `kukym1.tga`, `kukym2.tga` und `kukym3.tga` mit derselben
geliehenen Schiffskarte, Stärke 0,65 und `uv = 1`. Die NAP-Auflösung fand alle
drei Bindings ohne Diagnose. Im beleuchteten LancerEdit-Viewer erzeugt die
aktivierte Map sichtbare Details, die beim Abschalten verschwinden. Das feste
Modell-ROI maß RMSE 3,1638/255, 16.385 Pixel mit Kanalabweichung über 3 und
maximal 104. Damit ist die UV1-Auswahl durch DFM-Builder, Material und Shader
auch auf einem echten Retail-DFM visuell nachgewiesen. Screenshots, A/B-Paar
und verstärkte Differenz liegen unter
[`docs/evidence/renderer/`](evidence/renderer/); Kennzahlen und Prüfsummen sind
in [`dfm-normalmaps-ab-20261013.json`](evidence/renderer/dfm-normalmaps-ab-20261013.json)
zusammengefasst.
Auch diese geliehene Karte taugt nicht zur Qualitätsabnahme der Charakter-UVs;
passende Charakter-Normalmaps und eine Winkel-/Beleuchtungsmatrix bleiben offen.
