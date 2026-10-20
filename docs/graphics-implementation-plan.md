# Grafik-Erweiterungen: Umsetzungsplan

Stand: 2026-10-13. Dieser Plan verbindet die neu eingelesenen Dokumente
[Renderer-Roadmap](renderer-roadmap.md), [Surface Maps](surface-maps.md) und
[Vulkan-Untersuchung](vulkan-backend-untersuchung.md) mit dem aktuellen
Client-Quelltext und dem lokalen Data-Bestand. Die Erweiterungen sind damit
geplant. Sichtbare Surface-Normalmapwirkung ist im LancerEdit-Retail-Viewer
für `LI_ELITE.cmp` nachgewiesen; ein fertiges Vulkan-Backend sowie Spielbild-
und Qualitätsabnahmen stehen weiterhin aus.

## Aktueller Status (2026-10-13)

Dieser Status ersetzt die älteren Fortschrittsangaben und offenen P1/P2-Punkte
weiter unten, soweit sie den aktuellen Normalmap-Teilstrom beschreiben. Parser,
Profilwahl, Surface-Metadatenbindung, asynchrones CPU-Vorwärmen, CPU-/GPU-Cache
und opt-in BasicMaterial-Normalmaps sind integriert. Die Einstellung
`surface_normal_maps` ist gespeichert, standardmäßig aus und live umschaltbar.
`surface_generated_tangents` steuert fehlende VMesh-Tangenten aus NORMAL+TEX1/
TEX2 sowie DFM-Tangenten; es ist standardmäßig an. Surface-Karten benötigen
zusätzlich den Normalmap-Schalter. DFM-MAT-Normalmaps behalten ihren eigenen
Materialpfad und benötigen den Surface-Schalter nicht. So können Anwender die
automatische Mesh-Erweiterung abschalten und authored FVF3/FVF4-Tangenten
weiterverwenden. `[NormalMap] uv=0|1` wählt nun für ergänzende Normalmaps sowohl
den Tangentenaufbau als auch die Shaderkoordinate; Standard ist UV0. Authored
MAT-`NmSampler` behalten ihre eigene `SecondUV`-Flag. Beide Renderoptionen und
die UV-Zuordnung werden in der Settings-INI beziehungsweise Surface-INI geführt.

Der letzte vollständige Lösungsbuild umfasste 35 Projekte mit 0 Fehlern und
308 Warnungen. Die jüngste DFM-Änderung wurde über `LibreLancer.csproj` mit
Shaderkompilierung gebaut: 0 Fehler, 129 Warnungen (22 Projekt-Outputzeilen).
Sieben fokussierte CPU-Tests für DFM-Tangentengeometrie bestehen ebenfalls.
Die Patchserie rekonstruiert 446 Dateien; die DFM-Änderungen liegen in den
bestehenden Bündeln 2003/2009/2010 und erweitern die 13 aktiven Pakete nicht.
Der isolierte Skinned-Vertexshader-Probe besteht 9/9 Normalen-Readbacks
(maximal 0,005641 bei Toleranz 0,025). Eine getrennte Erweiterung mit den
Produktionsshadern von `BasicMaterial` und `CommandBuffer` besteht 13/13
zusätzliche Material-Readbacks (maximaler RGB-Fehler 0,002811 bei Toleranz
0,015), darunter opaque und deferred transparente synthetische DFM-Draws.
Enginehash und Build stimmen überein; beide Probeläufe beendeten den GPU-Shutdown
ohne ausstehende Ressourcen. Die Nachweise sind
[Skinned-TBN](evidence/dfm-skinned-gpu-readback-20261013.json) und
[Material/CommandBuffer](evidence/dfm-material-commandbuffer-gpu-readback-20261013.json).
Sie verwenden synthetische Geometrie, Knochenmatrizen und Texturen; echte
NAP-Materialauflösung, `DfmMesh`-Async-Cache und Animation sind damit nicht
abgenommen.

Der NAP-Lauf vom 13.10.2026 erreichte `m01a_fp7_01.thn` und lud die
Benutzeroberfläche. Dabei wurde `DcDtBtOcOtTwo` als nicht unterstützter
Materialtyp protokolliert. Die Parserliste wurde ergänzt; der fokussierte
Materialtyp-Test besteht 4/4 Fälle, der Client-Build 0 Fehler. Im erneuten
isolierten NAP-Lauf fehlen diese Parserfehler, und Spielszene sowie UI laden.
Eine Bildabnahme war in diesem damaligen Spieletest nicht verfügbar. Die
neuere LancerEdit-Retail-Abnahme weiter unten belegt inzwischen sichtbare
Normalmapwirkung auf `LI_ELITE.cmp`, aber noch keine Bildqualität im Spiel.

Der ältere [Data-Laufbericht](../../Data/docs/nap-runtime-assets-20261013.md)
meldet Nomad-Materialwarnungen, enthält dafür aber kein Rohlog. Mit dem
frisch über BuildEngine (0 Fehler) gebauten Client 03bfd87-git (20261013)
ist der NAP-Lauf jetzt
reproduziert: Ein verifiziertes Core-NAP (8.368 Einträge), Surface-NAP
(42 Einträge) und Runtime-NAP (10 Einträge unter EXE/) werden über
packages/active.json geladen. Das konfigurierte Installationsverzeichnis
enthält kein loses DATA. Der Client meldet Finished loading game data,
Interface loaded, betritt FP7_system, findet das THN-Hauptobjekt mehrfach
und läuft 79 Sekunden bis in den Missionsübergang zur Basis. Im Lauf gibt es
102 Warnungen, keine Error-Zeile und keinen DcDtBtOcOtTwo-Materialfehler.
Damit ist die frühere Warnung mit dem aktuellen Build/NAP nicht reproduzierbar.
Ein kontrollierter Bildversuch startet dieselbe NAP-Installation zweimal mit
`surface_profile=standard`, einmal mit `surface_normal_maps=true`, einmal mit
`false`. Beide Läufe laden die Spieldaten, betreten `FP7_system` und erreichen
denselben THN-Einstieg `m01a_fp7_01.thn`. Die sichtbare Freeport-7-Station nutzt
`solar\freeport7.mat`; Texturen wie `space_cap.tga` und `space_girder.tga` sind
in `SurfaceMapsSolar.ini` zugeordnet. Die 1280x720-Captures liegen lokal unter
`/tmp/lancer-renderer-nap-20261013/evidence/nap-ab-normalmaps-on.png` und
`nap-ab-normalmaps-off.png`. Die Kamera bewegt sich während der vier Sekunden
bis zur Aufnahme sichtbar weiter; das unregistrierte RMSE von 0,0397 kann daher
nicht der Normalmap zugeschrieben werden. Auch getrennte Läufe, die beim selben
THN-Einstieg nach einer Sekunde mit F1 pausieren, unterscheiden sich noch leicht
(RMSE 0,0132) und gelten nicht als Wirkungsvergleich. Die Pause selbst hält die
Szene stabil: Ein sichtbarer Stationsausschnitt war in zwei Bildern desselben
pausierten Laufs im Abstand von fünf Sekunden pixelgleich (RMSE 0); die lokalen
Aufnahmen liegen unter
`/tmp/lancer-renderer-nap-20261013/evidence/nap-pause-stable-{a,b}.png`.
Ein zusätzlicher Lauf schaltete im selben pausierten Client die Option von an
auf aus. Die Bilder, das verstärkte Differenzbild und die neu berechneten
Messwerte liegen jetzt projektlokal in der
[Vergleichsgalerie](renderer-comparison-gallery.md) und
[`ingame-direct-toggle-preliminary-20261013.json`](evidence/renderer/ingame-direct-toggle-preliminary-20261013.json).
In der Station-ROI (850,370)-(1100,620) änderten sich 10.253 Pixel bei
RMSE 0,434560/255; in der Himmelskontrolle waren es 47 Pixel und RMSE
0,080156/255. Das ist mit einem lokalen Stationseffekt vereinbar. Das Anwenden
setzte die Simulation kurz fort, Wiederholungsbilder desselben Zustands fehlen.
Eine spätere Optionsmenü-Aufnahme änderte nur 78 Stationspixel, weniger als die
274 Pixel innerhalb wiederholter OFF- oder ON-Aufnahmen. Zusammen belegen die
Läufe deshalb noch keine reproduzierbare In-Game-Materialbindung oder
Bildqualität. Kontrollierte Aufnahmen mit festem Simulationszeitpunkt sowie
CPU-/GPU-Kosten und weitere Materialklassen bleiben offen.

Der LancerEdit-Editorstart wurde mit Shaderkompilierung erfolgreich gebaut (0 Fehler; der inkrementelle Build meldete 77 Warnungen). Beim Öffnen von `LI_ELITE.cmp` erkennt der Model Viewer die passenden Ship-Archetype-Einträge über normalisierte VFS-Pfade, bevorzugt den eindeutigen Archetype `li_elite` anhand des Dateinamens und lädt `li_playerships.mat` sowie `envmapbasic.mat`. Der Retail-Model-Viewer zeigt dadurch die echten Schiffstexturen statt der pinken Missing-Material-Darstellung. Derselbe Viewer wurde anschließend mit `--data /tmp/lancer-renderer-nap-20261013/install` gestartet; dieses Installationsverzeichnis enthält nur EXE-Konfigurationen und kein loses DATA. Über `app/packages/active.json` wurden Core-, Surface- und Runtime-NAPs eingebunden. Der Paketverifizierer bestätigte 8.368 Core-, 42 Surface- und 10 Runtime-Einträge; `core.nap` enthält beide geladenen Materialbibliotheken (`SHIPS/LIBERTY/li_playerships.mat` und `FX/envmapbasic.mat`). Auch dieser Lauf zeigt das Schiff korrekt texturiert. Der NAP-Screenshot liegt lokal unter `/tmp/lancer-renderer-nap-20261013/evidence/lanceredit-nap-ship-final.png`, der Retail-Vergleich unter `/tmp/lancer-renderer-nap-20261013/evidence/lanceredit-retail-ship-final.png`. Damit ist die Retail-Materialbindung im Editor über den NAP-VFS belegt; die sichtbare Surface-Normalmapwirkung auf `LI_ELITE.cmp` wird im folgenden A/B-Nachweis geprüft, während die Bildqualität weiter offen bleibt.

Der Model Viewer hat im Render-Tab Live-Schalter für ergänzende Surface-Normalmaps und fehlende Tangenten; die Spieloptionen bleiben davon unabhängig. Im selben NAP-basierten Retail-Viewer wurde `LI_ELITE.cmp` im beleuchteten Modus bei unveränderter Kamera und neutralgrauem Umgebungslicht (RGB 129) einmal mit Normalmaps aus und einmal an aufgenommen. Das Paar liegt lokal unter `/tmp/lancer-renderer-nap-20261013/evidence/lanceredit-li-elite-lit-grayambient-normal-{off,on}.png`; generierte Tangenten waren in beiden Aufnahmen aktiv. Ein Pillow-Vergleich misst im Modellbereich einen RMSE von 19,77/255 und 61.100 Pixel mit einer Kanalabweichung über 3; der Hintergrundbereich bleibt pixelgleich. Die Änderung ist auf den Oberflächen des Retail-Schiffs sichtbar und bestätigt, dass die Normalmap im Viewer greift. Das belegt noch keine Bildqualität im Spiel: mehrere Blickwinkel, weitere Modelle, Materialabstimmung und GPU-Kosten bleiben offen.

Für eine Basis wurde `bw_pygar_airlock.cmp` aus dem Retail-DATA-Bestand mit dem NAP-Viewer verglichen. Die Surface-INI bindet drei echte Materialien (`Dig_site_18`, `Dig_site_20`, `Dig_site_16`) an passende Karten aus dem Katalog. Bei fester Kamera, Lit-Modus, neutralgrauem Umgebungslicht (RGB 128) und aktiven generierten Tangenten zeigen die Aufnahmen mit/ohne Surface-Normalmaps im Modellbereich RMSE 10,4163/255 und 70.735 Pixel mit Kanalabweichung über 3; der Hintergrundkontrollbereich bleibt unverändert. Die Wände zeigen sichtbare Detailänderungen. Das bestätigt den Basispfad, ist wegen des einzelnen Modells und Blickwinkels aber noch keine Qualitätsabnahme. Aufnahmen: `/tmp/lancer-renderer-nap-20261013/evidence/lanceredit-pygar-neutral-map-{off,on}.png`.

Der Planetenkandidat `planet_desert_800` in Bw04 (`Bw04_planet_Deidus`) hat eine echte Zuordnung zu `nm_pbody_desert01` auf UV0. Die 2D-Systemkarte zeigt eine texturierte Planetenvorschau: `GetArchetypePreview` rendert den Solar-Archetype, dessen `ResolvedModel` `MaterialPaths` über `LoadFile` lädt. Der Screenshot liegt unter `/tmp/lancer-renderer-nap-20261013/evidence/lanceredit-bw04-planet-2d.png` (vergrößerter Ausschnitt: `lanceredit-bw04-planet-preview-crop.png`). Auch die 3D-Systemansicht rendert den texturierten Planeten in Bw04, sobald die anfangs zu nahe Kamera herausgezoomt wird; die Szene zeigt dabei ebenfalls Stern, Nebel und Asteroidenfeld. Aufnahme: `/tmp/lancer-renderer-nap-20261013/evidence/lanceredit-bw04-planet-zoomout.png`. Patch 2014 erweitert die direkte rohe `.sph`-Vorschau: `LoadArchetypeLibraries` findet nun auch Solar-Archetypes über normalisierte Pfade und lädt deren TXM/MAT-Bibliotheken. Patch 2015 ergänzt Tangenten auf den sechs UV-Flächen der prozeduralen `QuadSphere` und bindet Surface-Normalmaps auch im `DetailMapMaterial`-Shader. Im NAP-Viewer wurde `desert01` aus `planet_desert.mat` der Karte `nm_pbody_desert01` zugeordnet. Bei fester Kamera, Lit-Modus und neutralem Umgebungslicht RGB 124 änderte das Live-A/B im selben Prozess im Planeten-ROI 490.048 Pixel um mehr als 2/255; die mittlere absolute RGB-Abweichung beträgt 7,20/255. Die Aufnahmen liegen unter `/tmp/lancer-renderer-nap-20261013/evidence/lanceredit-sphere-surface-normalmap-{off,on}.png`. Die konfigurierte Stärke 6 zeigt im Nahbild ein sehr ausgeprägtes Relief. Data bewertet diese Karte anhand ihrer Normalenstatistik als „gut“: die 95. Perzentile der Abweichung liegt bei 21,2° ([Data-Auswertung](../../Data/surface-maps-bewertung.md)). Das Nahbild allein rechtfertigt deshalb keine Änderung des Kartenfaktors. Die Bildqualität muss noch bei typischer Planetengröße, aus mehreren Blickwinkeln und im Spiel geprüft werden; Cube-Flächennähte und Tangentenausrichtung bleiben ebenfalls offen. Der frühere zu nahe Kamerastandpunkt war kein festgestellter Rendererfehler.

Der native VMesh-/Surface-Smoke vom 13.10.2026 besteht auf Mesa Intel HD 4600:
Der asynchrone Cache erzeugte drei FVF3-Ecken, `BasicMaterial` band die echte
BC5-NAP-Karte `nm_ships_k_fighter01`, und der GPU-Pixel wich vom Kontrolllauf
ohne Zusatzkarte ab. Bei Stärke 0,5 war der ausgewählte Pixel nur Ambientlicht;
Stärke 0 stellte die Basenantwort wieder her. Damit ist der Pfad aktiv, aber
Ausrichtung und UV dieses Kartenbereichs sind noch nicht visuell bewertet. Der
Cache meldete 1.398.096 residente Bytes und null Leases; nach VMesh-Dispose war
das 64-MiB-Tangentenbudget wieder leer. SDL3/OpenGL endete mit null offenen
GPU-Freigaben und null Shadern. Der Smoke nutzt ein synthetisches VMesh und ist
kein Retail-Mesh- oder Bildvergleich. Maschinenwerte:
[VMesh-Surface-GPU-Smoke](evidence/vmesh-surface-gpu-draw-20261013.json).

Die Karte `nm_ships_k_fighter01` wurde unabhängig aus dem Retail-TXM extrahiert und als BC5/ATI2 mit 1024×1024 Pixeln und neun Mip-Stufen dekodiert. Eine CPU-Referenz über alle Texel der obersten Mip-Stufe ergibt bei Stärke 0,5 im Mittel 7,27° und p95 17,72° Normalenabweichung; neun Texel liegen durch BC5-Kompression außerhalb des XY-Einheitskreises. Ein isolierter SDL3/OpenGL-Probe vergleicht nun denselben Rasterpunkt aus dem VMesh-Draw: Pixel (10,6), UV (0,44270834; 0,40104166), Linear/Repeat, Mip 0, Stärke 0,5. CPU-Beleuchtungswert (0,24894007; 0,42420343; 0,92256314), GPU-Readback (0,2509804; 0,42352942; 0,92156863); nach 8-Bit-Quantisierung höchstens 1/255 Abweichung. Der Normalmap-Shader und Texturslot 2 waren aktiv; gegenüber dem Draw ohne Karte änderten sich 748/1024 Pixel, maximal 100/255. Die frühere Diagnose mit abweichender DFM-Geometrie war ungültig und ist durch diesen Vergleich ersetzt. Maschinenwerte: [BC5-CPU-Referenz](evidence/vmesh-bc5-cpu-reference-20261013.json) und [VMesh-CPU/GPU-Sample](evidence/vmesh-surface-cpu-gpu-sample-20261013.json).

Nächste Arbeiten: kontrollierte In-Game-Bildpaare mit festem Simulationszeitpunkt
für Schiff, Basis, Planet und DFM abnehmen; für DFM eine passende Charakterkarte
verwenden. GPU-/CPU-Kosten, Tangentencache-Einstellungswechsel und Budgetmangel/
Retry sowie nichtuniforme Skalierungen bleiben offen. Das Vorzeichen negativer
World-Skalierungen wird jetzt in der TBN-Händigkeit berücksichtigt und die
Cull-Fläche für einseitige Draws umgekehrt; die visuelle GPU-/In-Game-Abnahme
dieses Falls bleibt offen. Die bisherige
DFM-Karte belegt nur den Pfad, nicht die Bildqualität. Details und Grenzen:
[DFM-Implementierungsstand](vmesh-tangents.md#dfm-tangenten-und-skinning-implementierung-2026-10-13).
Cubemap-/IBL-, Roughness-/Metallic-Sampler und LancerEdit-Herkunftsanzeige
bleiben eigene nachfolgende Pakete. Optionen für nicht implementierte Effekte
bleiben gesperrt.

## Ziel und Zuständigkeit

Vorhandene NAP-Normalmaps und Cubemaps sollen auf Schiffen, Basen und
Solar-Objekten tatsächlich wirken. Dazu kommen Roughness/Metallic, eine
geordnete Render-Pipeline und ein Vulkan-Backend mit vollständiger Spiel- und
Editor-Unterstützung. Die weiteren Effekte der Renderer-Roadmap werden über
dieselben Grundlagen umgesetzt und jeweils einzeln abgenommen.

Client/Engine verantwortet Parser, Materialien, Mesh-/Shaderpfade, Backends,
Editor und Tests. Data verantwortet Karten, Zuordnungs-INIs, Generatoren,
IBL-Vorberechnung und NAP-Inhalte. Laufzeitassets bleiben lokale Eingaben;
synthetische Fixtures und Metadaten dürfen in Git. Engineänderungen bleiben
als funktional gebündelte Patches unter `patches/series` erhalten.

## Renderer-Performance-Diagnose (2026-10-14)

Das Debugfenster sammelt jetzt bis zu 300 aktuelle Framewerte und zeigt p50,
p95 und p99 für zwei getrennte Messungen: Update+Draw+EndFrame bis zum Swap
sowie das gesamte Frame-Intervall einschließlich Swap/VSync. Die Werte werden
viermal pro Sekunde neu berechnet. SDL2, SDL3 und Null stellen die Messwerte
bereit. Die erste Messung ist CPU-seitige Wandzeit und kann Treiberwartezeit
enthalten; sie ist kein GPU-Timer. Die letzten bis zu 300 Samples lassen sich
aus dem Debugfenster als CSV exportieren. Backend und Fenstermaß werden je Frame
aufgezeichnet; das Backend-Feld wird CSV-sicher maskiert. GPU-Zeit und
Ressourcenmessung sowie dokumentierte Vorher/Nachher-Baselines für feste Szenen
bleiben offen. OpenGL kann bei sichtbarem Debug-Overlay zusätzlich asynchrone
GPU-Zeiten für Depth-Prepass, Opaque-, Sterne/Nebel-, Transparent- und AA-Resolve-
Aufrufe anzeigen. Der CSV-Export ergänzt die zuletzt verfügbaren asynchronen
GPU-Werte und ihr Sample-Alter pro CPU-Frame; noch nicht verfügbare Ergebnisse
bleiben leer. Diese Werte sind noch nicht durch einen Hardwarelauf gegen ein
externes GPU-Profiling-Werkzeug abgeglichen; das Null-Backend meldet sie nicht.

## Verifizierter Ausgangspunkt

| Bereich | Aktueller Befund | Konsequenz |
|---|---|---|
| Renderer | `OpenGL` und `Null`; keine SDL_GPU-Implementierung im Client | Vulkan als vollständiges zusätzliches Backend entwickeln |
| Material | MAT-Normalmaps bleiben erhalten; Surface-Normalmaps sind opt-in für authored FVF3/FVF4 und generierte VMesh-/DFM-Tangenten aus NORMAL+TEX1/TEX2. DFM-Materialien nutzen jetzt optional den PBR-Pfad über `PBR_Skinned` | LI_ELITE-Maps und DFM-Normalmapping sind im Viewer geprüft; sichtbare DFM-PBR-Abnahme im Spiel, Basis, Planet und weitere Materialklassen bleiben offen |
| Tangenten | `VMeshTangentBuilder` erzeugt asynchron FVF3/FVF4 aus VMesh NORMAL+TEX1/TEX2; GPU-Payloadbudget 64 MiB | DFM-Builder (List/Strip, UV0/UV1, Skinning) mit Viewer-A/B ergänzt; reale Spielszene, Charakterreferenz und Leistungsabnahme offen |
| Shader | `NormalMap.hlsl` begrenzt den BC5-Z-Radikanden und normalisiert das Ergebnis; Stärke ist auf 0..16 begrenzt | Retail-TBN-Ausrichtung und Beleuchtungswirkung weiter visuell prüfen |
| Cubes | Basic bindet Envmap auf Slot 3; PBR verwendet diesen Slot für Metallic und bindet dort keine Envmap | Reflexion und IBL mit eigenem PBR-Bindungsplan ergänzen |
| Surface-INI | `SurfaceMapsIni` liest die konfigurierte `[Data] surface_maps`-Liste über das VFS; LancerEdit kann ergänzende Zuordnungen validieren und in einer eigenen Workspace-INI speichern | Editor-UI und exportierten NAP mit passendem `surface_maps`-Eintrag im Client prüfen; Residency, alle Profile und weitere Materialklassen bleiben offen |
| NAP-Daten | Reparierter Bestand: 41 Surface-Dateien, 1.349.682.438 Bytes; 7.005 DDS-Texturen und INI-Knotenverweise ohne Strukturfehler; `surface.nap` mit 41 Einträgen verifiziert | Eine Retail-Schiffszuordnung sichtbar geprüft; komplette Katalog- und Szenenabnahme bleibt offen |

Die vier kuratierten Bibliotheken enthalten bereits **55 Normalmaps** und
zugehörige Roughness-/Metallic-Karten; die drei `envcube_*` sind vorhanden.
Die aktuelle SDK-Zuordnung enthält 55 kuratierte, 46 Schiffs-, 85 Solar- und
2620 Basis-Einträge. Alle vier NAP-Kopien der Zuordnungs-INIs sind derzeit
leer. `normalmaps_bases_bretonia_3.txm` ist unvollständig; der Massenbestand
darf deshalb noch nicht als vollständig geladen gelten.

Der reproduzierte Metadatenbefund steht in
[graphics-input-inventory-20261012.json](evidence/graphics-input-inventory-20261012.json).
Er prüft UTF/DDS-Struktur und Dateigrenzen, keine Bildqualität und keine
aktive Installation. Die Dokumentzahlen 2670 Karten/1,2 GB und 361
Shader-Varianten sind ältere Inventarstände und werden vor Umsetzung neu
ermittelt. Ein aktuelles `surface.nap` muss separat gegen `active.json`
verifiziert werden.

## Entscheidungen für die Umsetzung

- Normalmaps/Cubemaps werden zuerst auf dem vorhandenen OpenGL-Pfad
  nutzbar gemacht; dieselben Materialien müssen anschließend auf Vulkan
  funktionieren. Die vorhandenen NAP-Daten werden dabei weiterverwendet.
- Als Vulkan-Weg wird **SDL_GPU mit Vulkan-Treiber** vorbereitet. SDL3 und
  die HLSL/SPIR-V-Kette sind vorhanden. Der Spike in P5 muss die Eignung
  beweisen; bei nicht lösbaren API-Grenzen wird mit dokumentiertem Befund
  auf ein direktes Vulkan-Backend umgeplant, statt Teilfunktionen abzuliefern.
- Der Renderer bleibt Forward-basiert. Clustered Forward wird nach Messung
  ergänzt; Transparenz, Nebel, Partikel und vorhandenes Antialiasing bleiben
  vollständig abgedeckt.
- Qualitätsprofile: Original, Minimal (kuratierte Karten), Standard
  (zusätzlich Schiffe/Solar), Voll (zusätzlich Basen). Während der Einführung
  bleibt Original der Referenzstand; die anderen Profile sind auswählbar.
- Bestehende `Nm_name`/`Rt_name`/`Mt_name` und explizite Archetyp-Envmap-Werte
  haben Vorrang. Die INI ergänzt fehlende Zuordnungen; Stärke 0 deaktiviert
  den jeweiligen Effekt. Fehlende optionale Karten führen zu einer
  verständlichen Warnung und einem gültigen Material.

## Reihenfolge und Abnahme

| Paket | Ergebnis | Abhängigkeit | Nachweis |
|---|---|---|---|
| P0 | Vollständiges Daten-/Shaderinventar und Referenzbilder | keine | Dateigrenzen, NAP-Hashes, feste Szenen und Messbericht |
| P1 | Surface-INI und bedarfsgeladene Bibliotheken | P0 | Parser-/VFS-Tests und Auflösungsprotokoll |
| P2 | Normalmapping auf Retail-Meshes | P1 | Bilder für Schiff, Basis, Planet und DFM; Fehlerfälle |
| P3 | Cubemap-Reflexion, Roughness/Metallic und IBL | P2 | gerichtete Cube-Fixtures, Energie-/Roughness-Vergleich |
| P4 | Render-Passes, HDR, Tonemapping, Bloom und Blur | P0; Materialien aus P2/P3 | Pass-/Resize-Tests und CPU-/GPU-Zeiten |
| P5 | SDL_GPU/Vulkan-Spike und Shader-Bindungsformat | P0 | echte Pipeline mit Retail-FVF, BC5, Cube und Skinning |
| P6 | Vollständiger Vulkan-Spiel-/Editorpfad | P4/P5 | Szenenmatrix gegen GL, Lifecycle-/Readback-Tests |
| P7 | Erweiterte Beleuchtung und atmosphärische Effekte | P3/P4/P6 | Qualitäts-/Kostenmessung je Effekt |
| P8 | Decals, Parallax, SSR, Motion Blur, TAA/Upscaling | passende P4/P7-Grundlagen | Artefakt-, History- und Vergleichstests |
| P9 | Plattform- und Releaseabnahme | P1–P8 | Linux/Windows-Matrix, signierte Datenpakete, Rückfallpfade |

### P0 – Bestand und Referenzen

**Verifizierter Fortschritt:** Die Surface-Generatoren, SDK-Synchronisierung und
NAP-Prüfung sind im Data-Repository dokumentiert:
[Surface-Reparatur](../../Data/docs/evidence/surface-repair-20261012.json),
[Newsvendor-Reparatur](../../Data/docs/newsvendor-repair-20261012.md) und
[native Menü-Navigation](../../Data/docs/native-menu-navigation-20261012.md).
Der aktive Snapshot lädt ohne loses DATA. Die zusätzlich behobene
[SDL3-MIME-Lebensdauer](sdl3-clipboard-mime-lifetime.md) beseitigt die bestätigte
NativeBuffer-Startwarnung. P0 bleibt für profilbezogene Materialzuordnung,
feste Referenzszenen und CPU-/GPU-Messungen offen.

- [ ] SDK-INIs nach NAP synchronisieren; leere/trunkierte Karten mit den
  vorhandenen Data-Generatoren reproduzieren. Bereits gültige Assets
  erhalten. Zuordnungen und doppelte Diffuse-Namen je Profil ausweisen.
- [ ] BC5/BC4, DDS-Mipzahlen, sechs Cubeflächen, UTF-Datenlängen und alle
  INI-Ziele prüfen; `surface.nap` erstellen/verifizieren und den verifizierten
  aktiven Snapshot ohne loses `DATA` laden.
- [ ] Feste Kameras/Beleuchtung für `li_fighter`, `bw_pygar_airlock`, einen
  Gesteinsplaneten, eine DFM-Figur, Nebel/Kampf, Asteroidenfeld und
  LancerEdit-Vorschau festlegen. Original-/Minimal-/Voll-Bilder speichern.
- [ ] CPU-/GPU-Framezeiten, Drawcalls, Zustandskombinationen, Uploads,
  Texturresidenten und Pipeline-Kompilierung auf benannter Hardware messen.
  Leistungsbudgets daraus ableiten; keine ungemessenen FPS-Zusagen.

### P1 – Daten und Residency

**GPU-Cache-Fortschritt:** `SurfaceGpuTextureCache` teilt Uploads nach Datei und
UTF-Knoten und begrenzt logische Payloadbytes je ResourceManager auf 256 MiB.
Aktive und hinter einem GPU-Fence wartende Texturen bleiben im Gesamtbudget.
CPU-Leases müssen außerhalb des Renderthreads geladen, für den Upload an den
Renderthread übergeben und GPU-Leases bis zum letzten Draw gehalten werden.
`BasicMaterial` bindet die Normalmap jetzt bei aktivierter Option für
tangententragende FVF3/FVF4-Meshes und gibt die GPU-Lease nach dem Draw frei.
Generierte DFM-Tangenten sind an denselben Surface-GPU-Cache angebunden.
Ein Retail-NAP-A/B auf `LI_ELITE.cmp` zeigt inzwischen eine sichtbare lokale
Normalmapwirkung im LancerEdit-Viewer; eine Bildabnahme im Spiel bleibt offen.
Die synthetischen DFM-Skinning- und Material-Readbacks werden jetzt durch echte
`DfmMesh`-Viewer-Draws ergänzt. `li_male_elite_body.dfm` wurde mit einer
temporären QA-NAP-Zuordnung der drei Diffuse-Materialien zu `nm_li_elite_hull`
(UV0, Stärke 0,65) im Lit-Modus mit generierten Tangenten an/aus verglichen.
Die Oberflächendetails erscheinen bei eingeschalteter Karte und verschwinden
beim Ausschalten; der Modellbereich erreicht RMSE 2,96/255. Für UV1 wurde
`ku_kym_body_gen.dfm` verwendet. Alle sechs LODs enthalten UV1-Daten und
UV1-Indizes; die drei Körpertexturen wurden temporär an dieselbe Karte mit
`uv = 1` gebunden. Auch hier verschwindet der sichtbare Detailunterschied beim
Ausschalten (RMSE 3,1638/255 im Modell-ROI). Die vollständigen Screenshots,
A/B-Paare und 8x-Differenzen liegen unter
[`evidence/renderer/`](evidence/renderer/); Kennzahlen und Prüfsummen stehen
in [`dfm-normalmaps-ab-20261013.json`](evidence/renderer/dfm-normalmaps-ab-20261013.json).
Damit sind UV0 und UV1 des echten DFM-Tangent- und Shaderpfads visuell
durchlaufen. Die verwendete Schiffskarte ist geliehen und keine
Charakterreferenz; passende Charakterkarten, UV-Ausrichtung und Bildqualität
bleiben offen. Ein lokaler Bestandsabgleich fand in 309 DFM-Körper-Materialien
weder authored `Nm_name` noch eine Diffuse-Textur mit Katalogzuordnung, weshalb
der DFM im Spiel ohne QA-Mapping weiterhin keine ergänzende Normalmap verwendet.

**Materialfortschritt:** Die ausgewählte Surface-Metadatenauflösung ist an
`Material.Initialize`/`BasicMaterial` angebunden. 39 Tests bestätigen MAT-Vorrang,
fehlende Angaben und Ausnahmefälle. Der NAP-Lauf findet 24 Bindings an realen
Materialien. Surface-Texturen werden über den GPU-Cache geladen und im
BasicMaterial-Draw mit Lease-/Fence-Lebensdauer gebunden. Übrig bleiben die
Abnahme weiterer Retail-Materialklassen und die Messung von Uploads,
Residency und GPU-Kosten.

**Fortschritt vom 13.10.2026:** Der Parser, die konfigurierte Ladeliste und
case-insensitive Metadatenauflösung sind implementiert. 17 Tests prüfen auch
NAN/Infinity, Wertebereiche, Pfade, Mehrfachdefinitionen und NAP-Zugriff ohne
Bibliotheksöffnung. Der Client lädt den vollen Katalog mit 2.807 Normalmap-
Zuordnungen. Die benannte Profilwahl ist nun in den Performance-Optionen und der
Konfiguration implementiert (Original als Standard, Neustart erforderlich).
30 Profil-/Parsertests und ein nativer UI-/Neustarttest bestätigen Auswahl,
Speichern und Katalogumfang. P1 ist für Parser, Profilwahl und GPU-Bindung
umgesetzt; Retail-Abdeckung, Eviction unter Last und Speicher-/GPU-Messungen
bleiben offen.

- [ ] `SurfaceMapsIni` mit `[Texture]`, `[NormalMap]`, `[CubeMap]` sowie
  `[Data] surface_maps`-Ladeliste implementieren. Profilabhängig nur die
  vorgesehenen INIs einlesen. Groß-/Kleinschreibung, doppelte Einträge,
  unbekannte Schlüssel und begrenzte endliche Faktoren prüfen.
- [ ] Textur-/Materialzuordnung indexieren. Mehrdeutige Diffuse-Namen
  diagnostizieren; optional Bibliotheks-/Materialidentität ergänzen.
  Für kompatible Dubletten gilt die dokumentierte letzte Zuordnung.
- [ ] Bibliotheken über denselben NAP-VFS wie bestehende Assets öffnen.
  Erst beim benötigten Material laden; Residency-Budget, Referenzzählung,
  Eviction und Freigabe nach GPU-Fence vorsehen. Nicht alle Basisbibliotheken
  beim Start in GPU-Speicher übernehmen.
- [x] Im Editor Materialzuordnung, aufgelöste Karten, UV-Auswahl, Stärke und
  Profil anzeigen. Ergänzende Normal-/Roughness-/Metallic-Zuordnungen können
  bearbeitet, über vorhandene Texturknoten validiert und in einer separaten
  Workspace-INI gespeichert werden. „Reload Data“ wendet sie im Viewer an.
- [ ] Den aktiven UI-Ablauf abnehmen und den exportierten NAP im Client testen.
  Die Client-Konfiguration muss die exportierte Workspace-INI über
  `[Data] surface_maps` referenzieren.

### P2 – Meshes und Normalmapping

- [x] UV0/UV1 für ergänzende Normalmaps über `[NormalMap] uv=0|1` auswählen und
  dieselbe Auswahl für VMesh-/DFM-Tangenten sowie den Normalmap-Sampler nutzen.
  MAT-`NmSampler` behalten ihre eigene UV-Flag; Originaldateien bleiben
  unverändert.
- [x] `MaterialAnim` transformiert UV0 und UV1 in `Basic_FVF` und
  `Basic_Skinned`; damit animieren auch `SecondUV`-Sampler ihre gewählte
  Koordinate.
- [x] Der Normalmap-Tangentenrahmen übernimmt die signierte U-/V-Skalierung
  aus `MaterialAnim` in FVF- und Skinned-Shadern. Animierte Kachelung verändert
  damit auch die Normalensteigung; negative Skalierungen spiegeln die jeweilige
  Tangentenachse. Offset und UV-Kanalwahl bleiben unverändert. Echte UV1-
  VMesh-/DFM-GPU-Draws mit nichtidentischer Animation und ihre Bildabnahme sind
  weiterhin offen.
- [x] Echte LI_ELITE-Roughness- und Metallic-BC4-Karten einzeln und zusammen
  im Retail-VMesh/PBR-Pfad gegen dieselben skalaren Materialfaktoren prüfen.
  ATI1 wird als lineares RGTC1 geladen; beide Shader lesen den R-Kanal ohne
  sRGB-Umrechnung. [Bilder und Messdaten](evidence/renderer/surface-pbr-retail-20261014/li-elite-retail-pbr-maps-20261014.json).
- [x] Ergänzende Roughness-/Metallic-Maps über `pbr_uv=0|1` unabhängig von
  Normalmap und Tangentenbasis auswählen. Fehlt `pbr_uv`, wird rückwärts-
  kompatibel `uv` verwendet; MAT-`Rt`/`Mt` behalten ihre eigenen Flags.
- [ ] Den UV1-Pfad mit nichtidentischer `MaterialAnim` in echten VMesh-/DFM-
  GPU-Draws visuell abnehmen; die Shader berücksichtigen die signierte
  Animationsskalierung des Tangentenrahmens jetzt in FVF und Skinning.
- [ ] Tangenten mit konsistenter Händigkeit aus Position, Normale, Index und
  gewählten UVs erzeugen. Gespiegelte UVs, Naht-/Hartkantensplits und
  degenerierte Dreiecke abdecken. Zusätzliche Vertices dürfen 16-Bit-Indizes
  nicht überlaufen; Meshes bei Bedarf aufteilen. Bereits vorhandene gültige
  Tangenten weiterverwenden.
- [x] DFM-Tangenten je FaceGroup/UV erzeugen und im Basic-Skinned-Pfad anbinden;
  List/Strip-Winding, per-Corner-Seams und Originalgeometrie erhalten.
- [x] DFM-Vertexlayout und Skinned-TBN-Math: 9/9 isolierte GPU-Readbacks.
- [x] Synthetische `BasicMaterial`-/`CommandBuffer`-Abnahme: authored MAT-
  Normalmap, Basic-Skinned/Basic-FVF, Stärke 0/1/2, UV0/UV1, zwei opaque und
  zwei deferred transparente Draw-Fixtures in beiden Submission-Reihenfolgen;
  13/13 Material-Readbacks bestanden. Die vorherigen neun Skinned-TBN-
  Readbacks bleiben als separater Nachweis erhalten.
- [ ] Echte DFM-/NAP-Materialauflösung und Retail-Referenzbilder; tatsächlicher
  `DfmMesh`-Async-Upload, LODs, nichtuniforme Skalierung, die visuelle Abnahme
  negativer World-Skalierungen und animierte Multipart-Meshes abnehmen.
  Synthetische Fixtures gelten nicht als Ersatz.
- [x] BC5 XY linear lesen, Stärke anwenden, Z mit begrenztem Radikanden
  rekonstruieren und normalisieren. Echte NAP-BC5-Samples stimmen im
  OpenGL-Readback mit der unabhängigen CPU-BC4-/Bilinear-/Normalenreferenz
  überein ([Prüfbericht](surface-gpu-upload.md)).
- [x] TBN-Achsen und +Y-Kanal im Basic-FVF-Produktionsshader kontrollieren:
  Tangenten-QA-Dreieck mit NAP-geladenem LI_ELITE-BasicMaterial und gerichteten
  +X/+Y-Lichtkontrollen; jede passende Normalmap ändert 23.930 Pixel gegenüber
  der Neutralmap. Die Geometrie und Kontrollmaps sind synthetisch.
  [A/B und Messung](evidence/renderer/surface-normal-only-20261014/li-elite-tbn-axis-control-ab.png).
- [x] Das geladene LI_ELITE-Retail-VMesh mit NAP-Normalmap und gerichteten
  +X/+Y-Lichtern gegen den ausgeschalteten Schalter vergleichen: 7.252 bzw.
  4.949 geänderte Pixel. PBR-Maps und Cubemaps waren aus; Tangenten wurden
  generiert. [Bilder und Messung](evidence/renderer/surface-normal-only-20261014/li-elite-retail-tbn-directional-ab.png).
- [ ] TBN-Reihenfolge und +Y-Polarität zusätzlich mit einer unabhängig
  bekannten Retail-Referenz vergleichen. Roughness/Metallic ebenfalls linear
  lesen; Farbtexturen und Emission getrennt behandeln.
- [x] Reine Surface-Normalmap auf einem geladenen LI_ELITE im Basic-FVF-
  Pixellichtpfad nachweisen, ohne Metallic-/Roughness-Maps oder PBR-Pfad:
  3.914 geänderte Pixel im Offscreen-A/B bei `surface_pbr_maps=false`.
  [Rohbilder und Messung](evidence/renderer/surface-normal-only-20261014/).
- [x] Normalmap-Stärke 0 auf dem geladenen LI_ELITE gegen Surface-Normalmaps
  aus vergleichen: 0 geänderte Pixel; der Output ist bytegleich.
- [x] Basic-FVF-Fallback ohne Tangenten am Produktionsshader testen: ein
  synthetisches Dreieck nutzt das geladene LI_ELITE-BasicMaterial mit dessen
  Normalmap-Binding. Slot 2 bleibt ungebunden; Option an/aus ergibt bytegleiche
  GPU-Bilder ohne Ausnahme.
  [A/B-Nachweis](evidence/renderer/surface-normal-only-20261014/li-elite-no-tangent-ab.png).
- [x] Neutrale Kontrollkarte im Basic-FVF-Produktionsshader prüfen: Das
  geladene LI_ELITE-BasicMaterial aus dem NAP-Testcontainer zeichnet ein
  tangentenfähiges QA-Dreieck. Eine neutrale BGRA8-Karte ist bytegleich zum
  Lauf ohne Map; die geneigte Positivkontrolle ändert 23.930 Pixel. Die
  1×1-Kontrollkarte wurde im Speicher erzeugt, nicht aus NAP geladen.
  [A/B und Messung](evidence/renderer/surface-normal-only-20261014/li-elite-neutral-map-control-ab.png).
- [x] Neutralwertkontrolle mit echter BC5-Normalmap aus einem separat
  verifizierten NAP-Archiv und geladenem LI_ELITE-Retail-VMesh ausführen: Die
  Neutralmap ändert 704 Pixel (RMSE 0,039848), die Originalmap 1.110 Pixel
  (RMSE 1,062918); Stärke 0 bleibt bytegleich. Beide Manifeststände wurden
  vorab geprüft, die QA-NAP-Eingaben stammen aus `Data/NAP/`.
  [Bilder und Messdaten](evidence/renderer/surface-normal-only-20261014/li-elite-nap-normalmap-20261014.json).
- [x] BC4/BC5 getrennt von S3TC erkennen und bei nativer RGTC-Unterstützung
  komprimiert hochladen. Fehlt RGTC, erweitert der OpenGL-Pfad ATI1/ATI2 per
  CPU nach BGRA; BC5 belegt R/G in derselben Kanalzuordnung wie native RGTC.
  Der Residency-Check rechnet in diesem Fall mit 4 Bytes pro Pixel statt mit
  den kleineren Blockdaten. `RgtcTests` und `SurfaceGpuTextureTests` bestehen
  16/16 Fälle für Kanalbelegung, Randblöcke, beschädigte Eingaben und
  Fallback-Budget. Lokale Intel/Mesa-Hardware meldet OpenGL 4.6 sowie beide
  Kompressionsfamilien; ein Lauf auf einem Gerät ohne RGTC oder ein erzwungener
  OpenGL-Fallback bleibt deshalb noch offen.
- [ ] Den tatsächlichen GL-Upload bei deaktivierter RGTC-Capability per
  erzwungenem Fallback oder geeigneter Zielhardware visuell/readback prüfen.

### P3 – Cubemaps und PBR

Stand 2026-10-18: Der opt-in PBR-Pfad bindet den Specular-Cube an Slot 5,
diffuse Irradiance an Slot 7; Slot 6 bleibt für Shadow-Maps reserviert.
Dt/Et/Nm/Mt/Rt belegen Slots 0–4. Die Umgebung wird mit GGX-vorgefilterten
Specular-Mips nach Rauheit abgetastet; diffuse Irradiance stammt aus separaten
cosine-konvolvierten NAP-Cubes. Für den Split-Sum-BRDF verwendet der Shader den
analytischen Epic-Fit; dafür ist keine LUT-Textur vorgesehen. Der Produktions-
PBR-Shader, sechs Cube-Richtungen, NAP-Cubemaps, LI_ELITE-Draws und die
LancerEdit-Live-Vorschau wurden kontrolliert. Das belegt die Daten- und
Shaderpfade, aber noch keine korrekte Retail-Orientierung, ausreichende
Bildqualität oder GPU-Kosten im Spiel. Belege und Grenzen stehen im
[PBR-Cubemap-Probebericht](evidence/renderer/pbr-environment-20261014/cubemap-assets-and-binding-audit.md),
[IBL-Livevergleich](evidence/renderer/ibl-live-options-20261018/README.md) und
[ModelViewer-Vergleich](evidence/renderer/ibl-modelviewer-ab-20261017/README.md).

- [x] Produktions-PBR-ENVMAP-Shader mit synthetischem Cube an sechs Richtungen
  und den NAP-Cubes `envmapbasic`, `envcube_space`, `envcube_sun` und
  `envcube_nebula` durch MAT/DDS-Lader und GPU-Draw prüfen.
- [x] Die drei Surface-Katalog-Cubes bedarfsgesteuert laden und `[CubeMap]`
  `strength` an den Produktionsshader weitergeben.
- [x] LancerEdit-ModelViewer-Vorschau für Katalog-Cubes auf demselben
  Retail-Mesh bereitstellen, ohne Spiel-INI-Dateien zu ändern.
- [x] GGX-vorgefilterte Specular-Mips und separate cosine-konvolvierte diffuse
  Irradiance-Cubes aus `Data/NAP/` erzeugen und über `[CubeMap]`-Metadaten
  laden. Legacy-Cubes behalten Level-0-Specular und den letzten-Mip-Fallback.
- [x] Bindungskonflikte vermeiden: Dt/Et/Nm/Mt/Rt liegen auf Slots 0–4,
  Specular-Cube auf 5, Shadow-Map auf 6 und diffuse Irradiance auf 7. Die
  Split-Sum-BRDF nutzt den analytischen Epic-Fit; dafür ist keine LUT-Textur
  erforderlich.
- [x] Metallic-Fallback, Roughness-Untergrenze und energieerhaltender Ambient-
  Anteil umsetzen. Ohne expliziten Metallic-Faktor gilt 0; bei gebundener
  Metallic-Karte gilt standardmäßig der Kartenfaktor 1. Diffuse IBL und lokale
  Ambient-Beiträge folgen dem nichtmetallischen Materialanteil.
- [x] Den PBR-Shader an die Legacy-Materialmultiplikation angleichen: lineare
  Vertexfarbe moduliert nun die Materialalbedo und die finale Materialalpha wird
  mit `Oc` (inklusive OpacityMultiplier) skaliert. Der Shader-Eingang enthielt
  bereits Vertexfarbe; zuvor wurde sie nicht verwendet und `Oc` war ungenutzt.
  Der Compiler-/DFM-PBR-Bundle-Test prüft die Shader-Varianten. Eine visuelle
  Materialabnahme mit getönten bzw. halbtransparenten Retail-Assets bleibt offen.
- [ ] Retail-Orientierung, sichtbare Wirkung metallischer/rauer Flächen und
  GPU-Kosten mit fester Kamera im Spiel abnehmen. Vorhandene Offscreen- und
  ModelViewer-Bilder ersetzen diesen In-Game-Nachweis nicht.

### P4 – Pässe und Post-Processing

- [x] Übergang zu HDR/Tonemapping im bestehenden GL-Spielpfad geprüft. Der
  allgemeine OpenGL-Texturpfad kennt `RGBA16F`, der Systemrenderer rendert aber
  direkt ins Caller-Ziel bzw. in LDR-AA-Ziele; MSAA nutzt derzeit 8-Bit-Farbe.
  Zudem ist die Farbkonvention nicht einheitlich: Der PBR-Shader kodiert seinen
  Ausgang selbst mit `pow(1/2.2)`, während der Legacy-Pfad keine gemeinsame
  lineare Szenenausgabe festlegt. Eine HDR-Umstellung darf deshalb nicht nur das
  Render-Target austauschen. Der Quellcode-Audit hält Voraussetzungen und die
  Reihenfolge fest: [HDR-/Tonemapping-Übergangsaudit](evidence/renderer/hdr-tonemapping-transition-audit-20261016.md).
- [x] Typisierte IDs und erste abstrakte Lese-/Schreibzugriffe für die sechs
  gemessenen SystemRenderer-GPU-Phasen angelegt; Frame-Clear wird nun ebenfalls
  gemessen. Diese Metadaten steuern auch
  die GPU-Timer-Slots. Es ist noch kein Render Graph. Zielrollen,
  Attachmentformate und Load-/Store-Angaben sind hinterlegt. Die
  Szenenzielidentität wird vor/nach Szenenphasen und die SMAA-Stencil-Anforderung
  beim Binden geprüft. Statische Vertragsprüfungen erzwingen Lade-/Schreibabhängigkeiten und Read-before-
  write in Hauptpassfolge, SMAA-Kette und MSAA-Resolve-Zweig.
  Caller-Formate, Treiber-MSAA-Format und API-Laufzeitabgleich fehlen. Details im
  [Forward-Pass-Audit](evidence/renderer/forward-pass-order-audit-20261015.md).
- [x] Opaque-GPU-Timing über die tatsächlichen synchronen Materialdraws in der
  Geometrieausgabe gelegt; `CommandBuffer.DrawOpaque()` sortiert nur die
  transparenten Befehle und füllt Billboard-Indizes. Der CPU-Aufwand des
  Geometrieaufbaus wird dadurch nicht als GPU-Zeit ausgewiesen.
- [x] OpenGL-Zielformate und Load-/Store-Verhalten der aktuellen Screen-, SMAA-
  und MSAA-Pfade aus den Implementierungen abgeleitet. Die Verträge unterscheiden
  Caller-defined Ausgabeziele und bilden drei SMAA-Draw-Pässe sowie den MSAA-
  Farbresolve getrennt ab. Die Hauptphasen prüfen ihre Szenenziel-Bindung; SMAA
  prüft Zielrollen und angeforderte Stencil-Attachments. Beim Anlegen des AA-
  Ziels werden SMAA-Formate/Maße/Stencil sowie MSAA-Samplezahl/Maße geprüft.
  Die API-Abfrage des Caller-/Swapchainformats, das vom Treiber gewählte
  Desktop-MSAA-Format, Load-/Store-Abgleich gegen echte API-Aufrufe und
  Render-Graph-Ausführung stehen aus. `AntialiasTarget` prüft inzwischen auch
  die Farb-/Tiefen-/Stencil-Präsenz seiner tatsächlich gebundenen Backends.
  Zusätzlich leert SMAA jetzt vor dem stencil-maskierten
  Gewichtspass das Gewichtsziel auf transparent schwarz, um veraltete Pixel aus
  vorherigen Frames auszuschließen.
  Details im [Target-Attachment-Audit](evidence/renderer/render-target-attachment-contract-20261016.md).
- [ ] Pass-Ein-/Ausgaben, Farb-/Tiefen-/Stencil-Lebensdauer, Clears, Resolve,
  Resize und minimierte Fenster explizit machen; Debug-Marker und Timings
  hinzufügen. Offscreen-Widgets und Editor-Viewports einbeziehen.
  - [x] Der optionale Main-Camera-Depth-Prepass ist im typisierten
    Passvertrag als bedingt markiert und liest/schreibt nur `SceneDepth`; er
    beansprucht kein Farb-Attachment. Das ist weiterhin Metadatenvalidierung,
    kein ausführbarer Render Graph. Offscreen-Widgets und Editor-Viewports sind
    nicht abgedeckt. Siehe [Renderer-Roadmap](renderer-roadmap.md#1-render-pipeline-und-profiling).
- [x] Opt-in HDR-Zwischenziel und ACES-Tonemapping im GL-Pfad. `render_hdr`,
  CLI-Override und Effects-Checkbox steuern den Pfad; Half-Float-Ziele werden
  für SMAA und MSAA verwendet. Bei nicht unterstütztem Floatziel bleibt LDR aktiv.
- [x] HDR-Szenen-Fragmentausgaben und Clear-Farben vor dem Blending nach linear
  überführen. PBR schreibt seine lineare Beleuchtungsfarbe direkt; die 18 übrigen
  Fragmentshader verwenden einen gemeinsamen HDR-Schalter für sRGB-EOTF. Die
  UI wird nach dem `SystemRenderer` und somit nach dessen Tone-Map-Ausgabe gezeichnet.
  Shader- und RenderContext-Verknüpfung ist in
  [HDR-Linear-Ausgabe](evidence/renderer/hdr-linear-output-implementation-20261016.md)
  dokumentiert. Ein visueller NAP-Lauf war mangels NAP-Archiven nicht möglich.
- [x] Die System- und Nebel-Hintergrundfarbe wird vor dem HDR-Framebuffer-Clear
  mit derselben sRGB-EOTF linearisiert wie die Fog-Farbe im Materialshader.
  Alpha und der gesamte LDR-Clear-Pfad bleiben unverändert. Die reine
  Konvertierung ist getestet; ein kalibrierter Ingame-HDR/LDR-Hintergrundvergleich
  fehlt weiterhin. Siehe [HDR-Clear-Farbvertrag](evidence/renderer/hdr-clear-color-contract-20261018.md).
- [x] Im opt-in HDR-Pfad diffuse Texturen, MAT-/Materialfarben, Vertexfarben,
  diffuse/ambient Lichtfarben und Fog-Farbe vor der Beleuchtungs- und
  Fogberechnung linear behandeln. Der gemeinsame Pfad umfasst Basic,
  DetailMapMaterial, DetailMap2Dm1Msk2PassMaterial, IllumDetailMapMaterial,
  Masked2DetailMapMaterial, Atmosphere und AsteroidBand. Diese Shader geben
  danach ohne zweite EOTF lineare Werte aus. Die Konvertierung ist im LDR-Pfad
  eine Identität; vorhandene LDR-Clamps und die Alpha-Masken bleiben erhalten.
- [x] Die restlichen zehn Legacy-Fragmentshader linearisierten ihre RGB-Eingänge
  nun ebenfalls vor Multiplikation, Addition oder Interpolation: Sprite,
  NebulaMaterial, NebulaInterior, NebulaExtPuff, Nomad, SunSpine, SunRadial,
  ZoneVolume, Color und Navmap. Alpha bleibt unverändert. Bei HDR-Aus bleibt die
  bisherige LDR-Ausgabe unverändert.
- [ ] Prüfen, ob alle NAP-/Retail-INI-Licht-, Systemambient- und Nebelfarben
  tatsächlich sRGB-encodiert sind; diese Annahme ist derzeit aus dem
  BGRA8/komprimierten UNORM-Texturpfad und der Materialdatenbehandlung abgeleitet,
  nicht visuell bestätigt. HDR-Farbabnahme und kalibrierte NAP-Vergleichsbilder
  fehlen weiterhin.
  - [x] Der Sun-Ambient-Tint mischt HDR-Lichtfarben und Systemambient jetzt
    linear, berechnet Rec. 709-Luminanz im linearen Raum und kodiert das Ergebnis
    für den gemeinsamen Shader-Eingang zurück nach sRGB. Die Änderung folgt der
    bestehenden HDR-Shaderkonvention; sie beweist noch nicht unabhängig, dass
    sämtliche Retail-INI-Farben sRGB statt linear gemeint sind. Parser- und
    Retail-Belege mit den offenen Prüfschritten stehen im
    [HDR-Sun-Ambient-Nachweis](evidence/renderer/hdr-sun-ambient-linear-20261018/README.md).
- [x] Erste optionale HDR-Bloom-Stufe mit Helligkeitsschwelle bei 1.0,
  Viertelauflösung (vier bilineare Samples mitteln das gesamte 4×4-Quellfenster;
  aufgerundete Zielmaße schließen ungerade Bildränder ein) und separablem
  Gauß-Blur.
  `render_bloom`,
  `--render-bloom=on|off` und ein Effekte-Schalter steuern sie unabhängig; sie
  Die Belichtungskompensation `render_hdr_exposure` (-4 bis +4 EV, Standard 0) steuert die ACES-Eingangshelligkeit mit `exp2(EV)` und ist im Effekte-Menü sowie über `--render-hdr-exposure` einstellbar.
  Sie benötigt `render_hdr` und fällt bei nicht verfügbaren Bloom-Zielen auf HDR
  ohne Bloom zurück. Die aufwendigere mehrstufige Downsample-Pyramide,
  visuelle Kalibrierung bleibt offen. `render_bloom_threshold`
  (0–8; Standard 1) und `render_bloom_strength` (0–2; Standard 0,18) lassen
  sich in der Clientkonfiguration und über gleichnamige CLI-Parameter setzen.
  `render_bloom_radius` (0,25–4; Standard 1) steuert den Blur-Abstand;
  `render_bloom_resolution_scale` (0,125–1; Standard 1) steuert die Größe der
  Blur-Ziele und ist per Clientkonfiguration, CLI und Effekte-Regler einstellbar.
  `render_bloom_kernel_pairs` (1–4; Standard 2) steuert die bilinear
  kombinierten Gaussian-Tap-Paare und ist ebenfalls per Konfiguration, CLI und
  Effekte-Regler einstellbar.
  UI-Blur, Tiefenunschärfe und Cutscene-Radialblur behalten eigene begrenzte
  Einsatzfälle und Qualitätsschalter.

### P5/P6 – Vulkan über SDL_GPU

- [ ] Backendneutrale Vertex-/Wrap-/Depth-Aufzählungen, Screenshot-/Readback-
  Schnittstelle und backendabhängige SDL-Fenstererzeugung einführen.
  - [x] `DepthFunction` verwendet neutrale Enum-Werte; OpenGL bildet `Less` und
    `LessEqual` auf eigene Vergleichsoperatoren ab und wendet Laufzeitänderungen
    in `ApplyState` an. Drei fokussierte Mappings-/Validierungstests bestehen.
    `VertexElementType` und `WrapMode` sind nun ebenfalls backendneutral und
    werden explizit in GL-Werte übersetzt; einschließlich der Sampler-Cache-
    Initialisierung bestehen insgesamt dreizehn Regressionstests.
    Screenshot-/Readback ist jetzt backendneutral: SDL2/SDL3 lesen den Backbuffer über `RenderContext.ReadBackbuffer`; OpenGL sichert und restauriert das Read-Framebuffer-Binding, Null liefert einen definierten leeren Frame. Der SDL-Fenster-/Swapchain-Vertrag bleibt offen.
- [ ] SDL3-Mindestversion und native Bereitstellung pro Plattform festlegen;
  SDL2/GLES/Null-Pfade erhalten. Geräte-/Formatfähigkeiten beim Start prüfen
  und eine nicht verfügbare Auswahl verständlich auf einen gültigen Backend
  zurückführen. Ein leeres, minimiertes Swapchain-Ziel ist ein Normalfall.
- [ ] Shader-Bündel versionieren und pro Variante eine logische Slot-zu-
  Backend-Bindungstabelle erstellen. SPIR-V-Sets und DXIL/MSL-Bindungen je
  Stufe lückenlos verdichten, bevor weitere Formatübersetzungen stattfinden.
  GL-Namen/Slots bleiben in der Kompatibilitätstabelle erhalten. Uniform-
  Anzahl und Blockgrößen gegen die gewählte SDL-Version prüfen.
- [ ] Storage-Teilbereiche für Bones/Partikel/Beams über geprüfte Start-/
  Längenparameter in bestehenden Uniforms abbilden. Partikel-/Beam-Draws
  mit `first_vertex=0` und explizitem Basisindex implementieren.
- [ ] Aufzeichnenden Renderpfad mit Upload-Versionen implementieren:
  immutable Zustands-/Uniform-Snapshots, Transfer-Puffer, Ressourcen-Cycling
  und Frame-Fences. Uploads vor ihren abhängigen Draws ausführen; ein
  späterer Glyphen-/Texturupload darf frühere Draws nicht rückwirkend ändern.
  Passgrenzen für Copy/Clear/Blit/Targetwechsel korrekt erhalten.
- [ ] Pipeline-/Sampler-Caches nach Shader, Vertexlayout, Blend, Tiefe,
  Stencil, Cull, Fill, Depth-Bias, Primitive, MSAA und Zielformat erstellen.
  Cache-Aufbau, Invalidierung und vorgewärmte Varianten messen.
- [ ] Texturen inkl. BC4/BC5/Cube, Buffer/Streams, async Readback, MSAA-Resolve,
  SMAA-Stencil, 2D/Text, Nebel, Partikel, Beams, DFM, Sterne/Planeten,
  Asteroiden, Navmap, ImGui und sämtliche Editor-Vorschauen implementieren.
- [ ] Gegen GL vergleichen: Culling, Clip-/Depth-Konvention, Alpha-Test,
  Transparenzsortierung, Textorientierung und sRGB. Resize, Alt-Tab,
  Vollbildwechsel, wiederholte Vorschauen und Gerätefehler testen.
- [ ] Linux Vulkan auf verfügbarer Zielhardware und Windows Vulkan prüfen.
  SDL_GPU-D3D12/Metal gesondert abnehmen, bevor diese Treiber freigegeben
  werden; ein Linux-Erfolg ist dafür kein Nachweis.

Die Bindungsverdichtung ist eine aktuelle Anforderung der
[SDL-Shader-API](https://wiki.libsdl.org/SDL3/SDL_CreateGPUShader).
[Storage-Bindungen](https://wiki.libsdl.org/SDL3/SDL_BindGPUVertexStorageBuffers)
enthalten keinen Subrange-Offset; der bisherige Clientvertrag muss deshalb
im Shader erhalten werden. Copy- und Render-Pässe folgen den Grenzen von
[SDL_BeginGPURenderPass](https://wiki.libsdl.org/SDL3/SDL_BeginGPURenderPass).

### P7/P8 – Weitere Grafikpakete

- [x] Erste integrierte gerichtete Schattenstufe: optionaler `render_shadows`-Pass mit 1024²-Depth-Map, Sichtfrustum-Lichtkamera, opaken aktiven Model-Castern und 3×3-PCF-Receivern in Basic/PBR. CLI: `--render-shadows=on|off`; Standard aus. Der Release-Build umfasst die Shader-Varianten und separat erfasste Offscreen-Caster; NAP-Laufzeit-/Bildabnahme, GLES und Kaskaden fehlen weiterhin.
- [ ] Soft Particles auf festen NAP-Szenen visuell abnehmen: Der optionale
  `render_soft_particles`-Pfad erzeugt eine samplebare Szenen-Depth-Map,
  übergibt Tiefe und `render_soft_particle_distance` an Partikel- und Beamshader
  und fällt bei fehlgeschlagener Depth-Target-Allokation auf harte Kanten zurück.
  Shader-Sampling und der kombinierte HDR-Optionslauf sind vorhanden; ein
  isolierter Ein/Aus-Bildvergleich für Partikel und Beams, Letterbox-Viewports,
  transparente Überlagerungen sowie die GPU-Kosten fehlen noch.
- [ ] Volumetrischen Nebel und Lichtstrahlen ergänzen; lineare Tiefe,
  Transparenz und bestehende Freelancer-Nebelparameter erhalten.
- [ ] Gerichtete Schatten zuerst mit Kaskaden/PCF, anschließend zusätzliche
  Lichttypen.
  - [x] Sampleable Depth-only-Renderziele über `RenderTarget2D(..., SurfaceFormat.Depth)` ergänzt. OpenGL bindet die Textur als Depth-Attachment ohne Color-Read-/Write-Buffer; der Null-Backend-Pfad behält Rendermaß und no-op-Verhalten. GL/GLES-Readback und Depth-to-Depth-Blit nutzen den Depth-Puffer; Color-to-Depth-Blits werden abgewiesen. Der Release-Build und ein OpenGL-4.6/Mesa-NAP-Smoke-Lauf bestehen: FBO-Erzeugung, Depth-Clear und float-Readback liefern min/max 1..1. GLES, Shader-Sampling und PCF sind nicht geprüft; siehe [Depth-only-Smoke](evidence/renderer/depth-only-target-smoke-20261017/README.md).
  - [x] Shadow-Caster-Draws, Lichtmatrizen und Receiver-Shading in Basic/PBR integrieren; `DirectionalShadowCamera` fittet den distanzbegrenzten Sichtfrustum-Slice und verankert den Lichtblickpunkt im Welt-XY-Raum, damit das texelgesnappte Projektionszentrum Shadow-Shimmering bei Sub-Texel-Kamerabewegung vermeidet. Kamera-Fit und Sub-Texel-Stabilität bestehen automatisierte Tests. `ModelRenderer.RenderScale` wird in Main-, Depth- und Shadow-Cullingradien berücksichtigt, damit hochskalierte sichtbare Geometrie nicht vorzeitig aus diesen Pässen entfernt wird; siehe [RenderScale-Culling](evidence/renderer/scaled-model-culling-20261018.md).
  - [ ] PCF-/Bias-Kalibrierung, Offscreen-Caster-Abdeckung und Receiver-Bildqualität in festen Retail-Szenen visuell abnehmen. Die Tests beweisen keine Szenenbildqualität.
- [ ] Kurzlebige Effektlichter vervollständigen und messen: Mündungslichter aus `Gun.light_anim` sowie Explosionslichter aus `lgt_effect`/`lgt_radius` und `lgt_time_scale` sind vorhanden. Per-Objekt-Auswahl und Frame-Pool priorisieren nach geschätztem attenuiertem Lichtbeitrag; der Frame-Pool verwirft Quellen außerhalb ihrer Reichweite und ohne positiven Beitrag. Die maximale Zahl zugelassener transienter Frame-Lichter ist mit `dynamic_light_budget` auf 0, 8, 16, 24 oder 32 begrenzbar. Die maximale Zahl aktiver Effektlicht-Ereignisse ist mit `transient_effect_light_budget` auf 0, 32, 64 oder 128 einstellbar; beim Absenken werden die schwächsten aktiven Kandidaten entfernt. `lgt_range_scale` wird bis zum transienten Lichtobjekt durchgereicht, bleibt aber bis zur Semantikklärung ohne Laufzeitwirkung. Offen sind explizite Lichtdaten und hardpointgebundene Lebenszyklen für persistentes Feuer, allgemeine Sichtbarkeits-/Occlusion-Priorisierung und reproduzierbare NAP-Szenenmessungen. Siehe [Feuerinventar](evidence/effect-fire-light-input-inventory-20261014.json).
  - [x] Negative oder nichtendliche Frame-Deltas werden in `SystemRenderer.Update` auf null normalisiert, bevor Nebel, Partikeleffekte und transiente Lichter aktualisiert werden.
- [ ] Danach Laser-, Projektil- und Explosionseffekte überarbeiten: Beam- und Trefferdarstellung, Trails, Partikel, Farbverläufe sowie Größen-/Zeitverhalten aufeinander abstimmen und mit dynamischer Beleuchtung integrieren. Originaleffekte und einzeln abschaltbare Qualitätsoptionen erhalten; NAP-Referenzszenen mit Vergleichsbildern und CPU-/GPU-Zeit abnehmen.
  - [x] `vis_beam`-Querschnittsbasis bleibt bei vertikaler oder ungültiger Richtung endlich und orthonormal; Zugriffe auf den Beam-Puffer sind auf seine Kapazität begrenzt. Vier fokussierte Tests bestehen.
  - [x] Partikel-/Beam-Schreibzugriffe bleiben im 30.000-Einträge-GPU-Pool; leere Beam-Chunks erzeugen keinen ungültigen Draw. Vier Grenztests bestehen.
  - [x] `head_brightness` und `trail_brightness` steuern Beam-Alpha; nicht angegebene Werte erhalten zur Legacy-Kompatibilität volle Intensität. Parser-/Farbtests decken `BeamSpear` und `BeamBolt` ab.
  - [x] `BeamsBuffer` verwirft Geometrie außerhalb des Kamerafrustums und priorisiert bei 500 Elementen pro Typ sichtbare Kandidaten nach Nähe zur konservativen Beam-Bounds-Kugel. Ein Min-Heap hält den schwächsten Slot verfügbar; Nachrücken kostet logarithmische CPU-Arbeit. Bildqualität und GPU-Zeit bei Überlast bleiben offen.
  - [x] Projektil-Beams separat per `render_projectile_beams` und `--render-projectile-beams=on|off` schaltbar machen; Projektilsimulation und Partikeleffekte bleiben davon unabhängig.
  - [x] Partikelgeometrie separat per `render_particle_effects` und `--render-particle-effects=on|off` schaltbar machen; Update und Lebensdauer der Effekte laufen weiter.
  - [x] Kurzlebige `lgt_effect`-Lichter unabhängig von anderen dynamischen Lichtern per `render_transient_effect_lights` / `--render-transient-effect-lights=on|off` schaltbar machen.
  - [x] Waffen-Mündungslichter aus `Gun.light_anim` unabhängig per `render_weapon_muzzle_lights` / `--render-weapon-muzzle-lights=on|off` schaltbar machen.
  - [ ] Sichtprüfung der Beam-/Explosiondarstellung, Timing-/Partikelabstimmung und einzeln schaltbare Qualitätsoptionen anhand von NAP-Referenzszenen.
- [x] Die in `Freelancer.ini` konfigurierten `jump_effect`- und `gate_tunnels`-Dateien in `FreelancerData` optional in typisierte Modelle laden und `SystemObject.jump_effect` case-insensitiv zum Gate-Profil auflösen. Das Profil ist vom zugehörigen `GameObject` erreichbar.
- [x] Den konfigurierten `jump_tunnel_effect` beim lokalen Sprung sowie beim zugelassenen Cluster-Sprung am Spielerschiff starten. Der Client startet ihn vor einem möglichen Szenenwechsel; der Loop-Sound endet nach `jump_out_time + jump_out_tunnel_time`. Die Auflösung nutzt das nächstgelegene Sprungtor mit passendem Zielsystem.
- [x] Die per `glow_ring_effect`, `glow_ring_hp` und `glow_create_time` konfigurierten Gate-FX zeitgesteuert an den Hardpoints des Quelltores ausführen. Das Datenmodell bindet auch die `layer`-Child-Sections der Tunnel-INI korrekt ein.
- [ ] Gate-Tunnelgeometrie und Ankunftssequenz implementieren und Abflug-/Ankunftszeiten für lokale und Cluster-Transfers im Spiel abnehmen. Der Effektpfad besitzt den unabhängigen Startparameter `--render-jump-gate-effects=on|off`; Quellcodebefund siehe [Renderer-Roadmap](renderer-roadmap.md).
- [ ] SSAO mit Geometrie-/Nebelmasken und Forward+ mit vielen Lichtquellen
  implementieren; die feste Lichtgrenze erst nach Messung ersetzen.
- [ ] Instancing/Batching für Asteroiden, Partikel und wiederholte Objekte
  sowie Shader-/Pipeline-Warmup anhand der P0-Messung ausbauen.
- [ ] Decals für Einschläge/Markierungen, Z-Fighting-/Sortier-/Skinningregeln
  liefern. Parallax nur mit geeigneten Height-Daten entwickeln; eine
  Normalmap ist keine Höhenkarte.
- [ ] SSR mit Cubemap-Fallback; kameraweite Motion Blur mit Bewegungsvektoren
  und Disocclusion-Schutz. Partikelblur deckt diesen Pfad nicht ab.
- [ ] TAA nach Jitter-/History-/Motion-Vektor-Grundlagen, danach skalierbares
  Upscaling. Camera Cuts, Resize, HUD, Nebel und Transparenz auf Ghosting
  prüfen. Native Auflösung und SMAA bleiben auswählbar.

## Freigabegates und nächste konkrete Arbeit

- [ ] Pro Paket Parser-/Mathematik-/Lifecycle-Tests mit synthetischen Daten,
  fokussierter Shaderbuild, vollständige frische Overlay-Rekonstruktion und
  BuildLL-Build. GPU-Funktionen benötigen echte Frame-/Bildnachweise.
- [ ] Identische Szenen mit Original/Erweiterung und GL/Vulkan dokumentieren;
  CPU-/GPU-Zeit, VRAM, Uploadvolumen und Ladevorgänge mit dem Profil nennen.
- [ ] Fehlerfälle: leere/trunkierte NAP-Datei, fehlende Karte, UV-Degeneration,
  Format nicht unterstützt, Cache-Eviction, Resize und Geräteverlust.
- [ ] Updater-/NAP-Release mit geprüften Größen/Hashes und aktivem Snapshot;
  keine vollständige Grafikfreigabe aufgrund eines Compilererfolgs.

**Nächster Arbeitsschritt:** Eine reproduzierbare Kamera/Spielstands-Fixture
für denselben Retail-Draw mit Normalmaps an/aus festlegen, Bildpaare und
GPU-Zeiten aufnehmen und so die sichtbare Wirkung gegen korrekte UVs
abnehmen. DFM-Skinning und weitere Retail-Materialien bleiben offen. Danach
P3/P4 und der vollständige P5/P6-Vulkanpfad fortsetzen; synthetische
Fixtures und ein einzelner NAP-Start ersetzen diese Abnahmen nicht.

## Vorbereitete Grafikoptionen (2026-10-13)

Das Optionsmenü enthält nun Anzeige, Performance und Effekte. Bestehende
AA-/AF-/Per-Pixel-Einstellungen bleiben verfügbar. VSync lässt sich im
Anzeigebereich speichern und wird beim nächsten Start angewendet. Der
Surface-Schalter wählt weiterhin nur den Metadatenumfang; Original deaktiviert
das Laden der ergänzenden Surface-Kataloge, nicht vorhandene MAT-Eigenschaften.

Aktualisierung 2026-10-15: Der Performance-Tab schaltet Normalmaps,
Tangenten, Surface-Roughness-/Metallic-Maps und PBR-Cubemap-Reflexionen.
Der Effekte-Tab stellt die implementierten Schalter für dynamische Beleuchtung,
Mündungs- und Effektlichter, Projektile, Partikel, Lens Glow, Nebel und
Sternenhintergrund bereit. Frame-Lichtbudget (0/8/16/24/32) und aktive
Effektlicht-Ereignisse (0/32/64/128) sind getrennt einstellbar und werden in
`client.ini` gespeichert. Die Optionen werden beim Verlassen des Menüs
angewendet; Nebel, Sternenhintergrund und Projektil-Beams lesen den aktuellen
Wert pro Frame. HDR/Tonemapping, Bloom, Schatten, SSAO, weiche Partikel,
volumetrische Effekte, SSR, Parallax/Decals, Motion Blur sowie TAA/Upscaling
bleiben deaktivierte Platzhalter.

Der folgende Lauf dokumentiert den damaligen Optionsstand vom 2026-10-13,
bevor die jetzt verfügbaren Renderer-Schalter ergänzt wurden:

Bei Umsetzung des jeweiligen Pfads gehört dessen Aktivierung in dasselbe
Arbeitspaket: getrennte Optionen für getrennte Effekte, konservativer Default,
INI-Parsing/Write/MakeCopy/Apply, Geräte-/Backend-Verfügbarkeitsprüfung,
Qualitäts-/Stärkeregler mit validierten Grenzen und die tatsächliche Übergabe
an den Renderer. Neustartpflicht oder Live-Anwendung muss pro Option sichtbar
sein. Unsupported-Funktionen bleiben gesperrt. Auch das eingebettete
`interface.json` muss aus der XML/Lua-Vorlage neu kompiliert werden.

Verifiziert: vollständiger Solution-Build ohne Fehler, 41 Surface-/Settings-
Tests bestanden, UI-Compiler änderte ausschließlich options.lua und
options.designer.lua im eingebetteten Archiv. Im NAP-only-Linux-Testclient
wurden Anzeige/Effekte/Performance per nativer Maus geöffnet, VSync von true
auf false gespeichert, ein nativer Klick auf den gesperrten Bloom-Schalter
blieb wirkungslos und Escape/Exit funktionierten. Dies prüft die Options-UI,
keine neuen Renderwirkungen. Es bestehen weiterhin Build-Warnungen.

## Selektiver Surface-Texturleser (2026-10-13)

`SurfaceTextureReader.Read` öffnet einen `SurfaceTextureReference` über den
bestehenden VFS (einschließlich seekbarer NAP-Streams). Er liest maximal 8 MiB
UTF-Index und genau einen angeforderten MIPS-/CUBE-DDS-Payload, maximal 32 MiB.
Er lädt keine anderen Texturen und legt keine GPU-Ressourcen an. Direkte
Stream-Aufrufe lassen den Stream offen; VFS-Aufrufe geben ihn nach dem Lesen frei.
UTF-Bounds, Indexüberschneidungen, Zyklen, Namens-/Tiefenlimits und mehrdeutige
Payloads werden geprüft. Beide Indexblock-Reihenfolgen werden unterstützt.

Surface-DDS werden auf positive begrenzte Dimensionen, Mipzahl, exakte
Payloadlänge und sechs quadratische Cubeflächen geprüft. Unterstützt sind die
klassischen Engine-Blockformate DXT1/DXT3/DXT5/ATI1(BC4)/ATI2(BC5) und
unkomprimierte RGB/BGR24 sowie RGBA/BGRA32 mit Standard-Kanalmasken. Die
vorhandenen Surface-Cubemaps benötigen BGRA32. DX10, Volume- und andere
Pixelmasken sind für diesen Zusatzpfad nicht freigegeben.
Fehlerhafte oder fehlende Texturen werfen kontrollierte Ausnahmen; der spätere
Material-Loader muss sie diagnostizieren und das ursprüngliche MAT beibehalten.

Der Leser ist die CPU-seitige Ladegrundlage von P1. Bedarfsgesteuerte Aufrufe
beim Material, Residency-Budget, Referenzzählung, Eviction, GPU-Freigabe sowie
Sampler-/Shaderaktivierung bleiben offen. NAP kann beim Zugriff ganze komprimierte
Chunks dekomprimieren; die Begrenzung des Lesers betrifft seine UTF-/DDS-Puffer,
nicht den bereits separat begrenzten NAP-Chunkcache.

Verifiziert: 65 Surface-/Settings-Tests bestanden, davon 24 neue Leser-Tests.
Der finale Solution-Build hat null Fehler. Ein VFS-Probeprozess mit dem aktiven
NAP-Snapshot liest 55 reale Normalmaps (76.902.320 Payloadbytes) und alle drei
BGRA32-Cubemaps (je 2.097.272 Bytes) erfolgreich. Jeder Payload wurde bytegenau
mit dem bisherigen vollständigen UTF-Loader verglichen. CountingStream-Tests
belegen zusätzlich, dass die neue Methode nur Index plus angeforderten Payload
liest und einen 1-MiB-Nachbarpayload auslässt. Keine GPU-/Bildqualitätsaussage.

## Surface-CPU-Cache und Materialzugriff (2026-10-13)

Jeder Ressourcenmanager besitzt einen unabhängigen `SurfaceImageCache` mit
64 MiB DDS-Payloadbudget. Erst ein expliziter
`TryAcquireSurfaceImages(Material)`-Aufruf liest dessen aufgelöste Zusatzkarten
über den selektiven VFS-Leser. Normal-/Roughness-/Metallic-Kanäle teilen identische
Datei-/Node-Payloads, nicht bloß gleichnamige Nodes verschiedener Bibliotheken.
Library-Aliase, Groß-/Kleinschreibung und Pfadseparatoren werden beim Cache-Key
normalisiert. MAT-Priorität stammt weiterhin aus dem Materialresolver.

Leases pinnen die Payloads. LRU-Eviction und `ClearTextures` entfernen nur
unbenutzte Einträge; fehlende Karten und ein erschöpftes Budget führen zum
ursprünglichen Material zurück. Mehrkanalzugriffe geben bereits erhaltene
Leases bei Fehlern wieder frei. Dispose sperrt neue Zugriffe, lässt aktive
Leases gültig und gibt deren Daten nach der letzten Freigabe frei. Statistik
trennt residente Bytes, gepinnte Bytes, Einträge und aktive Leases. Ladezugriffe
sind synchron und im Cache serialisiert; vor Nutzung im Renderpfad gehört die
I/O-Arbeit auf einen Loader-Worker.

Das Budget begrenzt residente DDS-Payloads, keine Index-/Objektkosten,
vorübergehenden Ladepuffer oder den separat begrenzten NAP-Chunkcache. Der
Renderer ruft diesen CPU-Pfad noch nicht für aktive Drawcalls auf. GPU-Budget,
Upload, Referenzen bis zum Frame-Ende/GPU-Fence, Shader-/Tangentenaktivierung und
sichtbare Normalmaps bleiben offen. Die vorbereiteten Optionsschalter bleiben
entsprechend deaktiviert.

Verifiziert: 82 Surface-/Settings-Tests bestanden, darunter 17 Cache-/
Materialzugriffstests für LRU, Pinnen, Alias-Dedup, parallele Zugriffe,
Budgetgrenzen, Dispose und Fehler-Rollback. Der Ressourcenmanager fällt bei
fehlenden und korrupten Assets auf das ursprüngliche Material zurück. Der
finale Solution-Build hat null Fehler. Im NAP-only-Testclient fordern zwei
explizite Zugriffe auf ein reales Liberty-Material drei Payloads an:
1.748.000 residente/gepinnte Bytes, drei Einträge, sechs aktive Leases.
Nach Dispose beider Materialzugriffe und ClearUnused sind Bytes und Leases
null. Native Optionsnavigation, Escape und reguläres Exit bleiben erfolgreich.
395 Overlaydateien wurden vollständig aus der Patchserie rekonstruiert.

## BC5-/TBN-Shadergrundlage (2026-10-13)

Basic und PBR verwenden jetzt denselben `NormalMap.hlsl`-Helper: XY wird auf
signed normalisiert, Z mit sqrt(saturate(1-dot(XY,XY))) rekonstruiert. Eine
abschließende sichere Normalisierung verhindert NaNs an komprimierungsbedingten
Unit-Disk-Überschreitungen und bei Stärke null. `BasicMaterial.NormalStrength`
ist endlich und auf 0..16 begrenzt, Default 1; beide Materialuniforms übertragen
sie mit geprüftem C#/HLSL-Layout. Die Surface-Stärke wird noch nicht automatisch
in diesen Wert übernommen, solange GPU-Residency und Mesh-Tangenten fehlen.

Der Basic-FVF-Featurebezeichner wurde von NORMAL_MAP auf NORMALMAP korrigiert,
damit die bestehende Bitmaske 16 tatsächlich die #ifdef-Pfade aktiviert. TBN
enthält nun Tangente, Bitangente, Normale als Zeilen; der Fragmentpfad multipliziert
die Tangentennormale als Zeilenvektor damit. Welt-Tangenten verwenden World
anstelle der inversen Normalmatrix und werden zur Normalen orthogonalisiert.
Eine stabile Ersatzbasis behandelt degenerierte Tangenten; UV-Handedness bleibt
berücksichtigt. Negative World-Skalierung, UV-Seams, DFM/Skinning und neue
Retail-FVF-Tangenten müssen im weiteren P2-Paket gesondert geprüft werden.

[Shaderprüfung und Grenzen](normalmap-shader-validation.md): 89 relevante Tests,
Solution-Build ohne Fehler und sieben echte OpenGL-Readback-Fälle mit denselben
HLSL-Helpern. Dies ist kein Bildqualitätsbeleg auf Retail-Schiffen und aktiviert
keine Supplemental-Surface-Sampler. Die vorbereiteten Optionsschalter bleiben
gesperrt. Eine NativeBuffer-Warnung beim kurzlebigen Testshader zeigt zusätzlich
die noch fehlende explizite Shader-Dispose-Schnittstelle; die Shader-Lebensdauer
gehört in das GPU-Ressourcenpaket.

## Shader-Lebensdauer (2026-10-13)

Die beim kurzlebigen GPU-Test beobachtete GLShader-Uniformpufferwarnung ist für
die geprüften expliziten und normalen Shutdown-Pfade behoben. Shader und
ShaderBundle sind IDisposable; RenderContext registriert deren Shader und
schließt die Registry vor SDL-Quit. Freigabe/Erstellung erfolgt auf dem
Renderthread, Use-after-dispose wird abgewiesen. Bundle-Teilaufbau, GL-Compile-/
Link-Fehler und Context-Initialisierungsfehler besitzen Shader-Rollbackpfade.
`DisposeShaders` ist kein allgemeines RenderContext-Dispose.

94 Tests und ein echter NAP-only-OpenGL-Lauf bestätigen Bundlefreigabe,
Uniformpufferbilanz, glIsProgram=false nach dem Dispose des Probeprogramms,
owned=0/total=0 nach normalem Shutdown und weiterhin sieben Normalmap-Readbacks.
[Prüfbericht](shader-lifecycle.md). Der zuerst abgebrochene Headless-Testlauf
benötigte libblurgtext auf LD_LIBRARY_PATH; der finale Lauf ist vollständig grün.
Andere GPU-Ressourcen, Fences, Geräteverlust/Crash-Shutdown und Windows/GLES-
Laufzeitprüfungen bleiben offen. Die aktive Serie enthält weiterhin 13 Bundles.

## Retail-FVF-Tangenten (2026-10-13)

`VMeshTangentBuilder` verwendet die vorhandene managed MikkTSpace-Implementierung
in SimpleMesh. Für einen Materialdraw werden NORMAL+TEX1/TEX2 mit explizitem
VMeshRef-Basevertex und UV0/UV1 in ein getrenntes FVF3/FVF4-Layout erweitert.
Jede Dreiecksecke besitzt einen eigenen Vertex; gespiegelte UV-Gruppen können
unterschiedliche Tangenten/Handedness behalten. Der SimpleMesh-Adapter kehrt
Mikk-Handedness für seinen Exporter um; der Clientbuilder konvertiert sie zurück
zur kanonischen cross(N,T)*sign-Basis. Bestehende TEX3/TEX4 werden nicht verändert.
Ein Off-by-one in GetTexCoord weist nun auch coordinate==TexCoords ab.

104 Tests, ein fehlerfreier Solution-Build, 45 reale Liberty-Fighter-Draws mit
5.841 Ecken aus dem aktiven NAP sowie acht GPU-Readbacks bestätigen Layout,
Offsetauflösung und Basisrichtungen. [Prüfbericht](vmesh-tangents.md).
Der GPU-Test lädt die erzeugten Bytes und Indizes mit dem bestehenden FVF-
Vertexshader; SampleXY im Testfragment ist ein Uniform und keine BC5-Textur.
Tangenten-/Vertex-Cache, Sampler-Anbindung, Uploadbudget/Fences, Referenzbilder,
DFM/Skinning und MaterialAnim-Skalierungen bleiben offen. Negative
World-Skalierungen berücksichtigen nun das Determinantenvorzeichen in der TBN;
deren visuelle GPU-Abnahme bleibt offen.
Die Grafikschalter für Zusatzkarten bleiben bis zur fertigen Rendereranbindung
weiterhin gesperrt. Die aktive Patchserie bleibt bei 13 funktionalen Bundles.

## Surface-GPU-Upload und BC5-Sampler (2026-10-13)

Ein expliziter `SurfaceGpuTexture`-Upload validiert DDS und logische GPU-Payload-
Bytes einschließlich RGB24-Expansion vor Allokation, prüft den Renderthread und
verwendet den vorhandenen DDS-Backendpfad. CPU-Leases werden nicht übernommen.
Budgetmangel bzw. fehlende Kompressionsunterstützung liefern null. Mipflags
müssen mit der DDS-Loaderinterpretation übereinstimmen; kaputte Header/Payloads
werden abgewiesen. DDS-Uploads geben nach bereits erfolgter Texturerzeugung bei
folgenden Fehlern die Textur frei; Standard-RGB/RGBA-Masken werden korrekt in
BGRA konvertiert.

115 Tests, fehlerfreier Solution-Build und neun echte BC5-Sampler-Readbacks
bestanden: acht bekannte Testblöcke für FVF3/FVF4 sowie eine NAP-Normalmap mit
1.398.096 GPU-Payloadbytes. Der Echttexturpixel stimmt mit einer unabhängigen
CPU-BC4-Palette/Bilinear-/Normalenreferenz überein. [Prüfbericht](surface-gpu-upload.md).
Der erste Echttexturvergleich hatte im separaten Probehost einen nicht gesetzten
MaterialAnim-Skalierungsuniform und schlug korrekt fehl; nach Initialisierung
wie im normalen BasicMaterial-Pfad wurde der ganze Lauf erfolgreich wiederholt.

Dies ist der explizite Upload-/Samplerpfad, kein globales GPU-Residencybudget und
keine aktive Supplemental-Materialanbindung. Mesh-/Texturcache, Loaderqueue,
Frame-/Fence-Retirement, Geräteverlust und visuelle Referenzszenen bleiben offen.
Nicht implementierte PBR-, Cubemap- und Postprocessing-Schalter bleiben deaktiviert.

## GPU-Freigabe am Frame-Ende (2026-10-13)

Eine renderthreadgebundene GpuFrameRetirement-Queue sammelt Freigaben bis EndFrame.
OpenGL setzt einen Fence hinter die Frame-Kommandos und flusht ihn; QueryFences
führt Freigaben nur nach Signal aus. Ohne Sync wird synchron auf GPU-Abschluss
gewartet. Der normale Shader-Shutdown schließt/entleert die Freigabequeue zuerst.
SurfaceGpuTexture.RetireAfterFrame sperrt neue Scope-Zugriffe; folgendes Dispose
umgeht die verzögerte Freigabe nicht. Retry-/Callback-Fehler bleiben sichtbar.

124 Tests, ein fehlerfreier Solution-Build und eine aktuelle SDL3/OpenGL-Prüfung
mit einer tatsächlich gezeichneten BC5-Textur bestätigen die verzögerte Freigabe
und den Drain einer im Cleanup neu erzeugten Textur. [Prüfbericht](gpu-frame-retirement.md).
Die frühere temporäre NAP-Testumgebung war vor diesem Schritt entfernt worden;
dieser Lauf ist deshalb ausdrücklich ein nativer Einzeltest ohne NAP-Spielstart.
Gemeinsamer Mesh-/Texturcache, Residencybudget, Loaderqueue und aktive Material-
Anbindung bleiben nächste P1/P2-Aufgaben. Geräteverlust und Recovery sind offen.

## Normalmap-Bildnachweise und einzelne Startoptionen (2026-10-14)

Das stärkste visuelle Paar stammt aus LancerEdit: Retail `LI_ELITE.cmp`, feste
Kamera, Lit-Modus, neutrales Umgebungslicht RGB 129, generierte Tangenten in
beiden Zuständen; nur die Surface-Normalmaps unterscheiden sich. Im
Modellbereich (790x420 Pixel) ändern sich 69.494 Pixel; RMSE 20,62/255. Der
Hintergrund-Kontrollbereich ist pixelgleich. Originalbilder, Zuschnitt und
Differenz liegen in [`evidence/renderer/`](evidence/renderer/).

Der Ingame-Kontrolllauf schaltete die Option im pausierten FP7-Intro aus und an;
die gespeicherte Einstellung wurde anschließend wieder auf `false` gesetzt.
Bei festem Cursor und geöffnetem Optionsmenü änderten sich im Stationsausschnitt
78 Pixel bei RMSE 0,022/255, maximal 1/255 pro Kanal. Wiederholungen desselben
Zustands variierten durch Menü-/Hintergrundanimation um etwa 274 Pixel bei RMSE
0,19/255. Der OFF/ON-Unterschied liegt damit unter der Messvariation und belegt
keinen sichtbaren Ingame-Effekt. Eine passende Ingame-Materialzuordnung
und die GPU-Kosten bleiben offen. Messwerte, Prüfsummen und Grenzen stehen in
`surface-normalmaps-comparison-20261014.json`.

Jede implementierte Renderoption kann beim Start einzeln überschrieben werden.
Nicht angegebene Optionen behalten den Wert aus der Clientkonfiguration. Die
verfügbaren Parameter sind `--render-normal-maps=on|off`,
`--render-surface-pbr-maps=on|off` und `--render-pbr-environment-maps=on|off`,
`--render-generated-tangents=on|off`,
`--render-surface-profile=original|minimal|standard|full`,
`--render-per-pixel-lighting=on|off`,
`--render-dynamic-lighting=on|off`, `--render-dynamic-light-budget=0|8|16|24|32`,
`--render-transient-effect-light-budget=0|32|64|128`, `--render-nebulae=on|off` und
`--render-starsphere=on|off`,
`--render-antialias=none|smaa-low|smaa-medium|smaa-high|smaa-ultra|msaa-2x|msaa-4x|msaa-8x`
und `--render-anisotropy=0|2|4|8|16`. Nicht unterstützte AA- und
Anisotropiewerte werden nach der Backend-Erkennung automatisch deaktiviert.
PBR-Cubemap-Reflexionen sind separat und standardmäßig aus über
`--render-pbr-environment-maps=on|off` steuerbar. Der PBR-Pfad nutzt
GGX-vorgefilterte Specular-Mips und separate diffuse Irradiance, wenn der
Katalog diese Metadaten bereitstellt. Legacy-Cubes bleiben kompatibel und
verwenden Level 0 für Specular sowie den letzten Mip als diffuse Näherung.
`--render-pbr-diffuse-ibl=on|off` und `pbr_diffuse_ibl` schalten den diffusen
Irradiance-Beitrag separat von Specular-Reflexionen. Der Schalter ist
standardmäßig an und wirkt nur, wenn PBR-Umgebungsreflexionen aktiv sind.
Dynamische Schatten, HDR und weitere Post-Processing-Pässe sind inzwischen
implementiert; deren jeweilige Retail- und Hardwareabnahme ist separat in der
[Renderer-Roadmap](renderer-roadmap.md) dokumentiert.

Beispiele:

```sh
lancer client.ini --render-normal-maps=on --render-dynamic-lighting=off
lancer client.ini --render-nebulae=off --render-starsphere=on --render-antialias=smaa-high
```

Der Material-Probe-Lauf im FP7-Intro fand 73 aufgelöste Surface-Bindings und
35 tatsächlich gezeichnete Normalmaps. Sichtbar gebundene Beispiele sind
`g_fighter01` und `g_fighter03`; die Shaderpfade erhalten Map-Referenz,
UV-Kanal und Stärke. Die Testinstrumentierung wurde nach dem Lauf entfernt.
Offen bleiben ein fixes Ingame-A/B mit einer gezielt ausgewählten sichtbaren
Retail-Textur sowie die CPU-/GPU-Zeitmessung.

Der Render-Tab im LancerEdit Model Viewer zeigt die Materialzuordnungen der
geöffneten CMP-/VMesh- und DFM-Modelle. Pro Eintrag werden Materialtyp, diffuse
Textur, authored MAT-Normalmap oder ergänzende Surface-Map mit Bibliothek,
Knoten, UV-Kanal und Stärke sowie vorhandene Roughness-/Metallic-Werte
angezeigt. Die Übersicht liest ausschließlich den aktiven ResourceManager und
ändert weder MAT-Dateien noch Surface-INIs. LancerEdit bietet nun eine
gespeicherte Surface-Profilwahl; sie wird beim Laden oder Neuladen der
Spielinhalte angewendet. Der Viewer zeigt das tatsächlich geladene Profil und
Katalogdiagnosen. Der Editor wurde gebaut, aber noch nicht im laufenden UI
visuell abgenommen. Material-INI-Bearbeitung und Workspace-Speichern bleiben
offen.
