# Surface Maps: Normal Maps und Cube Maps per INI

Die aktuelle Umsetzungsreihenfolge und Abnahme stehen im
[Grafik-Umsetzungsplan](graphics-implementation-plan.md).

**Status:** Parser, Profile, VFS- und CPU-/GPU-Residency sowie opt-in Normalmap-Bindungen für authored FVF3/FVF4, generierte VMesh-/DFM-Tangenten und QuadSphere sind umgesetzt. Ergänzende Roughness-/Metallic-Sampler sind über `surface_pbr_maps` angebunden. Ein kontrollierter LI_ELITE-A/B im LancerEdit ModelViewer bestätigt eine sichtbare Änderung. Im In-Game-A/B des vollständigen Profils bleiben nach Erhalt der Room-Vertex-Beleuchtung und korrekter Behandlung von Backface-Normalen die Stadtfassaden sichtbar beleuchtet; der zuvor beobachtete großflächige Schwarzeffekt tritt nicht mehr auf. Der Surface-Map-Lease-Absturz in Cutscenes ist ebenfalls behoben. Der opt-in PBR-Cubemap-Pfad löst `[CubeMap]`-Katalogeinträge nun anhand des vorhandenen `envmap_material` auf, lädt ihre MATs bei Bedarf und reicht `strength` weiter. Kontrollierte OpenGL-Proben mit Produktionsshader und echten `Data/NAP`-Assets bestätigen die drei Katalogwerte und Mip-Sampling. Der LI_ELITE-ModelViewer-A/B ändert trotzdem keine Modellpixel; sichtbare In-Game-Reflexionen, Retail-Orientierung, IBL-Qualität und Leistung bleiben offen. Vergleichsbilder und Messdaten liegen in der [Renderer-Galerie](renderer-comparison-gallery.md) und im [PBR-Cubemap-Probebericht](evidence/renderer/pbr-environment-20261014/cubemap-assets-and-binding-audit.md). Materialqualität in weiteren Szenen bleibt offen. **Stand:** 2026-10-14.
**Datenseite:** Repository `LN-Data` (`tools/make_surface_maps.py`, `SDK/fx/SurfaceMaps.ini`, `TODO-surface-maps.md`).
Engine-Änderungen bleiben gemäß `AGENTS.md` patch-only unter `patches/series`.
Der LancerEdit-ModelViewer kann einen Cubemap-Katalogeintrag nur für die
Vorschau überschreiben; die Auswahl schreibt nichts in die Archetyp-INIs.

## Aktuell implementierte Metadaten-Anbindung

```ini
[Data]
surface_maps = fx\SurfaceMaps.ini
surface_maps = fx\SurfaceMapsShips.ini
surface_maps = fx\SurfaceMapsSolar.ini
; Für den vollständigen Datenbestand zusätzlich:
surface_maps = fx\SurfaceMapsBases.ini
```

`FreelancerIni.SurfaceMapPaths` erhält die ausdrücklich konfigurierte Liste.
`FreelancerData.LoadData` lädt sie über das bestehende VFS nach
`FreelancerData.SurfaceMaps`. Ohne diese Einträge bleibt der Katalog leer.
Die gewählte Dateiliste bestimmt derzeit den Umfang; die benannte Profilwahl erfolgt in den Performance-Optionen oder über
`[Settings] surface_profile = original|minimal|standard|full`. Original ist
der Standard. Änderungen werden gespeichert und gelten nach einem Neustart.

`SurfaceMapsIni` liest Bibliotheken, Normalmap-Zuordnungen und den Cube-Katalog,
validiert relative Assetpfade und skalare Felder und löst Namen ohne Beachtung
der Schreibweise auf. Bei mehrfachen Definitionen gewinnt der letzte gültige
Eintrag; unbekannte Schlüssel, fehlende INIs und ungültige Abschnitte erzeugen
Diagnosen. `TryResolveNormalMap` liefert nur eine Metadatenbindung und meldet
fehlende Bibliotheksdeklarationen als nicht auflösbar.

Implementierte Zahlenbereiche: `strength` für Normalmaps 0–16, Cube-Stärke 0–1,
Roughness-/Metallic-Faktoren 0–1, jeweils endlich. Nicht angegebene PBR-Faktoren
bleiben ausdrücklich ungesetzt; sie aktivieren dadurch noch keinen PBR-Pfad.
Normalmap- und Cube-Stärke haben den Metadatenstandard 1. Bibliotheksdateien
und Texturknoten werden beim Metadatenladen nicht geöffnet oder auf die GPU
übertragen. Materialvorrang, Tangentenbasis, Texturauflösung, Residency sowie
die sichtbare Wirkung von Normal-, Roughness- und Metallic-Maps sind inzwischen
implementiert und teilweise mit Retail-Daten geprüft. `uv` steuert die ergänzende
Normalmap; optionales `pbr_uv` wählt unabhängig davon die Roughness-/Metallic-
Maps. Der FVF- und Skinned-Normalmap-Tangentenrahmen folgt nun auch der
signierten UV-Skalierung aus `MaterialAnim`. Offen bleiben insbesondere
visuelle GPU-Draws mit UV1 und abweichender `MaterialAnim`, DFM-/LOD-Abnahme,
breite Materialqualität und Performance.

Nachweise: 17 Parser-/VFS-Tests sowie ein Client-Lauf mit der vollen Liste aus
NAP (34 Bibliotheken, 2.807 Normalmap-Zuordnungen, 3 Cubes, keine Diagnosen).
Die alten Auto-Kartenzahlen weiter unten sind ein historischer Datenstand;
aktueller Reparaturstand im [Grafikplan](graphics-implementation-plan.md).
[Prüfbefunde und Assembly-Hashes](evidence/surface-metadata-20261013.json).

### Profilumfang und Konfiguration

| Profil | Gewählte kanonische INIs | Bibliotheken | Normalmaps | Cubes |
|---|---|---:|---:|---:|
| Original | keine | 0 | 0 | 0 |
| Minimal | SurfaceMaps.ini | 4 | 55 | 3 |
| Standard | zusätzlich Ships und Solar | 8 | 186 | 3 |
| Full | zusätzlich Bases | 34 | 2807 | 3 |

Die Tabelle wurde mit dem reparierten NAP-Bestand geprüft. Die konfigurierte
`surface_maps`-Liste bleibt maßgeblich: Das Profil ergänzt keine nicht
aufgeführten Dateien. Explizite benutzerdefinierte INIs mit anderen Dateinamen
sind gemeinsame Metadaten jedes aktivierten Profils. Original überspringt alle
Quellen. Unbekannte oder numerische Konfigurationswerte fallen auf Original
zurück. Die schema-interne API behält Full als Standard für bestehende
Metadaten-Werkzeuge; der Spiel-Client setzt ausdrücklich sein Benutzerprofil.

Der Optionsselektor zeigt Original/Minimal/Standard/Full und einen Neustart-
Hinweis. Er ändert die gespeicherte Auswahl, ohne laufende Welt- oder
Materialdaten neu aufzubauen. Native UI-Prüfung: Original → Standard,
gespeichert; nach Neustart 186 Normalmaps. Beide Client-Läufe endeten über
Exit mit Code 0. 30 Profil-/Parsertests bestehen. Materialwirkung und GPU-
Residency sind dadurch noch nicht umgesetzt.

[Profil-Prüfbefunde](evidence/surface-profiles-20261013.json).

### Materialauflösung: Metadaten an MAT und BasicMaterial

`GameDataManager` stellt den gewählten Katalog am `ResourceManager` bereit,
bevor Ressourcen vorab geladen werden. `Material.Initialize` löst für reguläre
Basic-Materialien anhand von `Dt_name` eine `SurfaceMaterialBinding` auf und
reicht sie an `BasicMaterial.SurfaceBinding` weiter. Kopierte grafische
Ressourcenmanager behalten dieselbe Katalogreferenz.

Die Binding-Deskriptoren ergänzen ausschließlich fehlende MAT-Angaben:

- Ein vorhandenes `Nm_name`, `Rt_name` oder `Mt_name` bleibt maßgeblich.
- Eine vorhandene MAT-Map besitzt auch ihren Faktor; die Surface-INI ersetzt
  diesen nicht, wenn die MAT keinen ausdrücklichen Faktor angibt.
- Ausdrückliche MAT-Roughness-/Metallic-Faktoren bleiben erhalten.
- Normalstärke 0 ergänzt keinen Normalmap-Verweis. Nicht angegebene Faktoren
  bleiben ungesetzt. Nicht auflösbare Bibliotheksdeklarationen ergeben kein Binding.
- Glass-/HUD-Materialien werden nicht ergänzt.

Diese Metadatenstufe öffnet keine Surface-Bibliotheken. Beim Draw kann der
separate opt-in Pfad eine zugeordnete Normalmap asynchron laden und über GPU-
Cache/Frame-Fence binden, sofern geeignete Tangenten vorliegen. VMesh-/DFM-
Tangenten und QuadSphere sind implementiert. Roughness-/Metallic-Sampler nutzen
`surface_pbr_maps` und bleiben vom Normalmap-Schalter unabhängig. Ein LI_ELITE-
A/B im Lit-Modus des ModelViewers zeigt bei aktiviertem Schalter eine deutliche
Änderung im gerenderten Modell; Hintergrund-Kontrollpixel blieben unverändert.
Das bestätigt den Preview-Pfad, aber weder die endgültige Materialqualität in
einer Spielszene noch die GPU-Kosten. Der separate Cubemap-Pfad verändert im
Offscreen-Produktionsdraw des geladenen LI_ELITE 13.160 Pixel je Katalog-Cube;
Reflexionsqualität, ModelViewer-Auswahl und vollständige Spielabnahme bleiben
offen. Der Binding-Resolver bleibt unabhängig von Residency.

39 Material-/Profil-/Parsertests bestehen. Im nativen NAP-Client wurden 24
passende Bindings beobachtet, darunter Liberty-MAT-Materialien mit
`elite_256.TGA` → `nm_li_elite_hull`. Der Menüablauf endet mit Exit-Code 0.
[Material-Prüfbefunde](evidence/surface-material-binding-20261013.json).

## 1. Ziel

Zu vorhandenen Texturen sollen zusätzliche Normal Maps und Cube Maps (Umgebungsspiegelung) in einer INI eingetragen werden können, ohne die binären `.mat`-Dateien zu ändern. Gilt für Schiffe, Stationen/Basen sowie Planeten und Solar-Objekte. Die Daten stehen als kleiner Beispielsatz bereit (55 Normal Maps, 3 Cube Maps). Die Stärken sind so abgestimmt, dass 95 % der Pixel höchstens 28° neigen (Bericht `LN-Data/surface-maps-bewertung.md`); das ist eine Festlegung der Datenseite, kein Wert aus der Engine.

## 2. Dateien

### 2.1 Normal Maps (Klasse `Surface`, virtueller Pfad `fx/…`): `Surface/fx/normalmaps_ships.txm`, `_bases`, `_solar`, `_planets`

- UTF-Texturbibliotheken, Aufbau wie jede `.txm`: Wurzel `\` → `Texture library` → ein Verzeichnis je Karte → Blatt `MIPS` (DDS).
- 55 Karten: `ships` 22, `bases` 12, `solar` 6, `planets` 15. Knotennamen `nm_<name>` (z. B. `nm_li_elite_hull`, `nm_bw_pygar_door`, `nm_planet_rock01`, `nm_pbody_mercury01`); die vollständige Liste steht in `fx/SurfaceMaps.ini`.
- DDS: 1024 × 1024, **BC5 / ATI2N** (`dwFourCC = 'ATI2'`, `SurfaceFormat.Rgtc2`), Kanäle R = X, G = Y, Mips bis 4 × 4 (9 Stufen). Kein DX10-Header. B ist nicht gespeichert, der Shader rekonstruiert Z.
- Inhalt: tangentiale Normalen, `x`/`y` auf `0..255` abgebildet (`0,5` = 0), **+Y oben** (OpenGL-Konvention), Z nach außen. Die Mips sind aus Vektoren gemittelt und renormiert. Die Detailkarten der Planeten (`planet_*`) sind kachelbar (Naht geprüft), Schiffs-, Basis- und Planetenkörperkarten (`pbody_*`) sind es nicht.
- Herkunft: aus der Helligkeit der Diffuse-Textur abgeleitet (Sobel auf geglätteter Höhenkarte). Keine echte Geometrie.

### 2.2 Cube Maps: `Surface/fx/envcube_space.mat`, `envcube_sun.mat`, `envcube_nebula.mat`

- UTF wie `envmapbasic.mat`: Wurzel → `Texture library` → Knoten mit dem Namen der Karte (`envcube_space` usw.) → Blatt `CUBE` (DDS).
- DDS: 256 × 256 je Fläche, A8R8G8B8, volle Mip-Kette, `dwCaps2 = 0xFE00` (Cube + alle sechs Flächen). Flächenfolge im Datenstrom: `+X, -X, +Y, -Y, +Z, -Z`, je Fläche alle Mips nacheinander (so liest sie `DDS.GetTextureCube`).
- Inhalt: prozedural (Sterne, Sonnenscheibe, Nebelwolken), keine echten Systeme.
- Der Knotenname ist der Wert für `envmap_material`.

### 2.1a Automatisch erzeugte Karten für alle Schiffe und Basen

Zusätzlich zu den 55 kuratierten Karten gibt es 2670 automatisch erzeugte, in `fx/SurfaceMapsShips.ini` (46), `fx/SurfaceMapsBases.ini` (2620) und `fx/SurfaceMapsSolar.ini` (85) verzeichnet (gleiche Syntax):

- Schiffe: `Surface/fx/normalmaps_ships_auto_1.txm`, `_2.txm` (49 Karten, 1024 px).
- Basen: `Surface/fx/normalmaps_bases_<generic|liberty|bretonia|kusari|rheinland|pirate|backdrops>_<n>.txm` (2621 Karten, 512 px), je höchstens 50 MB, zusammen rund 0,95 GB (BC5). Jede Datei hat einen eigenen `[Texture]`-Block mit eigenem Bibliotheksnamen.
- Solar: `Surface/fx/normalmaps_solar_auto_1.txm`, `_2.txm` (85 Karten: Sprungtore, Trade-Lane-Ringe, Docks, Weltraumstationen, Asteroiden, Gestein und Trümmer). Asteroiden und Gestein haben Roughness hoch und `metallic = 0`.
- Eine Karte gilt für eine Textur gleichen Namens; Namen mit verschiedenem Inhalt wurden nicht aufgenommen (144). Eine Zuordnung nach Material (`Dt_name` plus Materialname) würde das beheben; die INI kennt bisher nur den Texturnamen.
- **Lade-Profile:** minimal = `SurfaceMaps.ini`, Standard = + `SurfaceMapsShips.ini` und `SurfaceMapsSolar.ini`, voll = + `SurfaceMapsBases.ini`. Die Engine liest nur die INIs des gewählten Profils.
- Die Engine soll die gewählten INIs lesen. Bibliotheken sollten erst beim ersten Zugriff geladen werden (Speicher), das ist eine offene Entscheidung (siehe 5 und 6).

### 2.1b Roughness- und Metallic-Karten

- In denselben Bibliotheken wie die Normal Map: Knoten `rt_<name>` (Roughness) und `mt_<name>` (Metallic).
- DDS **BC4 / ATI1N** (`'ATI1'`, `SurfaceFormat.Rgtc1`), ein Kanal, Mips bis 4 × 4. Der Shader liest `.r` und multipliziert mit dem Faktor (`getRoughness`, `getMetallic` in `PBR.frag.hlsl`); Roughness wird auf mindestens 0,04 geklemmt.
- Größe: Schiffe 512 px, Basen und Solar 256 px. Planeten haben keine.
- Ableitung aus der Diffuse-Textur (Helligkeit, Sättigung, Detail), siehe `LN-Data/TODO-surface-maps.md`, Abschnitt 1c. Schätzung, keine Materialdaten.
- Ohne Metallic-Karte steht `metallic = 0` in der INI. Der Engine-Standard ist sonst 1.
- Ein LI_ELITE-Retail-VMesh hat die echten BC4-Karten getrennt und gemeinsam im PBR-Pfad gerendert. Roughness allein ändert 7.188 Pixel, Metallic allein 7.090 Pixel; der GPU-Cache hält danach zwei Einträge mit zusammen 349.520 Bytes. Bilder und genaue Laufbedingungen stehen im [Retail-PBR-Nachweis](renderer-comparison-gallery.md#li_elite-retailmesh-roughness-und-metallic-getrennt).
- `uv` steuert ergänzende Normalmap und Tangentenbasis. `pbr_uv` wählt unabhängig den UV-Kanal für Roughness-/Metallic-Karten; fehlt der Schlüssel, übernimmt er rückwärtskompatibel den Wert von `uv`. MAT-eigene `Rt`-/`Mt`-Sampler behalten weiter ihre eigenen `SecondUV`-Flags.
- Shader und OpenGL-Upload behandeln diese Karten linear: `getRoughness`/`getMetallic` lesen `.r`, die DDS-ATI1-Karten werden als `GL_COMPRESSED_RED_RGTC1_EXT` geladen. Diffuse-Albedo wird davon getrennt explizit in `SRGBtoLinear` umgerechnet.

## 3. INI-Syntax: `fx/SurfaceMaps.ini`

```ini
[Texture]                        ; Bibliothek der Normal Maps, je Gruppe eine
file = fx\normalmaps_ships.txm
name = surface_normalmaps_ships

[NormalMap]
texture  = elite_256.tga         ; Dt_name der Diffuse-Textur (Groß-/Kleinschreibung egal)
library  = surface_normalmaps_ships ; Name aus [Texture] (je Gruppe: ships, bases, solar, planets)
map      = nm_li_elite_hull      ; Texturknoten in der Bibliothek
strength = 2.5                   ; Faktor auf die Normalenabweichung, 0 = aus, 1 = unverändert
uv       = 0                     ; UV-Kanal für Normalmap und Tangentenbasis (Standard 0)
pbr_uv   = 1                     ; optionaler UV-Kanal für Roughness-/Metallic-Maps; fehlt = uv
roughness_map = rt_li_elite_hull ; optional, Einkanalkarte (R) in derselben Bibliothek
roughness     = 1                ; Faktor auf die Karte
metallic_map  = mt_li_elite_hull ; optional
metallic      = 1                ; Faktor; ohne Karte 0 (Engine-Standard wäre 1)

[CubeMap]
nickname = envcube_space         ; Knoten in der .mat = envmap_material
file     = fx\envcube_space.mat  ; = material_library
kind     = space                 ; space | sun | nebula (Hinweis)
strength = 0.35                  ; Vorschlag für die Stärke der Spiegelung (0..1)
prefiltered = true               ; Mip-Kette enthält GGX-vorgefilterte Specular-Stufen
irradiance_file = fx\envirradiance_space.mat ; diffuse Umgebung, cosine-konvolviert als E/pi
```

Regeln für die Auflösung (Vorschlag):

1. `[NormalMap]` gilt für jedes Material, dessen `Dt_name` gleich `texture` ist. Ein vorhandenes `Nm_name`, `Rt_name` oder `Mt_name` im `.mat` hat Vorrang, die INI füllt nur Lücken. Mit `roughness_map` oder `metallic_map` wird der PBR-Pfad des Materials benutzt.
2. Mehrere `[NormalMap]` für dieselbe Textur: der letzte Eintrag gewinnt, der Rest wird gewarnt.
3. Fehlt die Bibliothek oder die Karte, bleibt das Material unverändert (Warnung im Log, kein Fehler).
4. `prefiltered` ist optional und standardmäßig `false`. Nur `true`/`1` kennzeichnet GGX-Roughness-Mips. Legacy-Cubes mit gewöhnlichen Mips samplen für Specular weiter Level 0.
5. `irradiance_file` ist optional. Verweist es auf einen Cube mit cosine-konvolvierter diffuser Umgebung, nutzt PBR diesen für den diffusen IBL-Anteil; andernfalls bleibt die letzte Cubemap-Mipstufe als Legacy-Fallback aktiv. Der Generator speichert `E/pi` in einer kompakten 16×16-Karte.
6. `[CubeMap]` ist ein **Katalog**. Gebunden wird weiter über `material_library` und `envmap_material` in `ships\Shiparch.ini` und `solar\SolarArch.ini`.
7. Der Original-Client ignoriert die Datei, solange sie nicht in der `Freelancer.ini` verlinkt ist.

## 4. Was die Engine heute tut (Client-Quelltext, nur gelesen)

| Bereich | Stelle | Befund |
|---|---|---|
| Normal Map im Material | `LibreLancer/Utf/Mat/Material.cs` (Lesen von `Nm_name`, Zuweisung `bm.NmSampler`) | Wird gelesen und an `BasicMaterial` gereicht. |
| PBR-Shader | `Render/Materials/BasicMaterial.cs` (`GetPBRShader`, Feature `NORMALMAP`) | Wird gewählt, sobald `Nm` gesetzt ist; der PBR-Pfad selbst wird aber nur bei `Rt`/`Mt`/`Roughness`/`Metallic` benutzt (`pbr = (…) && lights.Enabled`). |
| Normal im PBR-Shader | `Shaders/PBR.frag.hlsl` (`getNormal`) | Liest `xy`, **rekonstruiert `z`**, nutzt die dritte UV-Menge (`GetTexCoord(2, …)`) und eine TBN-Matrix aus dem Vertex-Shader. |
| Normal im Standard-Shader | `BasicMaterial.GetRegularShader` | `NORMALMAP` nur bei `fvf.Normal` und `TexCoords` = 3 oder 4. |
| Cube Maps | `BasicMaterial.Use` (`GetTexture(3, envmaptex) is TextureCube`), `Archetype.EnvmapMaterial` → `ModelRenderer.EnvMapMaterial` → `CommandBuffer` | Cube wird per `envmap_material` gebunden, Feature `ENVMAP` im Standard-Shader. |
| DDS | `LibreLancer.Base/ImageLib/DDS.cs` (`GetTextureCube`) | Flächenfolge `+X -X +Y -Y +Z -Z`, Mips je Fläche. Der 2D-Pfad gibt für Cube `null` zurück. |
| INI-Laden | `LibreLancer.Data/Schema/FreelancerData.cs`, `Freelancer.ini [Data]` | Keine Stelle für eine zusätzliche Material-INI. |

Daraus ergeben sich Randbedingungen, die vor einer Umsetzung zu klären sind:

- **Tangenten:** Freelancer-Modelle haben (nach unserem Kenntnisstand, nicht geprüft) weder Tangenten noch die dritte UV-Menge. Ohne eigene Erzeugung läuft die Normal Map auf Originalmodellen nicht.
- **Zwei Shaderpfade:** Normal Map allein bewegt das Material nicht in den PBR-Pfad. Entweder der Standard-Shader oder die PBR-Bedingung wird erweitert.
- **Rekonstruktion von Z:** Der Shader nutzt nur die Kanäle R und G. Die Karten sind deshalb als BC5 gespeichert (nur X und Y), das passt zur Engine.
- **Stärke:** Weder `strength` der Normal Map noch die Reflexionsstärke der Cube Map ist in `BasicMaterial` vorgesehen.

## 5. Aufgaben

> Status 2026-10-15: INI-Auflösung, Materialbindung und GPU-Texturcache sind im Client integriert. Ergänzende Normal Maps laufen über `surface_normal_maps`; ergänzende Roughness-/Metallic-Maps und ihre skalaren Werte über den getrennten Schalter `surface_pbr_maps`. Der MAT-Eintrag hat Vorrang. DFM-Materialien können nun ebenfalls den PBR-Pfad nutzen; geskinte DFM-Meshes erhalten dafür den neuen `PBR_Skinned`-Vertexshader. Release-Build und NAP-only-Clientstart laden den Shaderbundle, eine sichtbare DFM-Materialabnahme im Spiel steht noch aus. LI_ELITE-ModelViewer und In-Game-A/B-Bilder liegen vor. Die Korrektur für Room-Vertex-Beleuchtung und Backface-Normalen beseitigt den zuvor beobachteten Schwarzeffekt im Liberty-Stadtbild. Der Produktionsdraw des geladenen LI_ELITE verändert bei Auswahl der Katalog-Cubemaps 13.160 Pixel gegenüber `envmapbasic`; die sichtbare Änderung bleibt subtil. Interaktive Auswahl, vollständige Spielabnahme, IBL-Qualität und Materialabnahme in weiteren Szenen bleiben offen.

Ein separater LI_ELITE-Offscreen-A/B bestätigt außerdem die reine Surface-
Normalmap im Basic-FVF-Pixellichtpfad bei ausgeschalteten PBR-Maps: 3.914 Pixel
ändern sich. Damit ist die Bindung nicht von einer künstlichen Metallic- oder
Roughness-Karte abhängig. Eine Normalmap-Stärke von 0 liefert pixelgenau das
Bild des ausgeschalteten Schalters. Das Basic-FVF-Material zeichnet auch ohne
Tangenten unverändert und bindet Slot 2 nicht; dieser Fallback wurde als
synthetischer GPU-Draw geprüft. Eine BGRA8-Neutralwertkarte im Basic-FVF-
Produktionsshader bleibt mit dem NAP-geladenen LI_ELITE-BasicMaterial pixelgenau
gleich zur Map-frei-Kontrolle; die geneigte Positivkontrolle ändert 23.930 Pixel.
Die Karte wurde nur im Speicher erzeugt. Gerichtete +X/+Y-Kontrollen mit
passenden Tangentnormalen ändern je 23.930 Pixel und bestätigen die Shader-
Achsenbelegung. Das geladene LI_ELITE-Retail-VMesh mit echter NAP-Normalmap
ändert unter +X/+Y-Licht 7.252 bzw. 4.949 Pixel. Die Polarität der Asset-Normalen
braucht noch eine unabhängige Referenz. Eine BC5-Neutralmap aus einem separat
geprüften NAP wurde nun auf dem Retail-VMesh geladen: Sie ändert gegenüber der
Map-freien Kontrolle 704 Pixel bei RMSE 0,039848; die Originalmap ändert 1.110
Pixel bei RMSE 1,062918. Stärke 0 bleibt bytegleich. Die visuelle Abnahme im normalen Client bleibt offen. Echte Roughness- und
Metallic-BC4-Karten wurden getrennt und zusammen auf dem Retail-VMesh geprüft;
der damalige A/B teilte noch den `uv`-Kanal. `pbr_uv` wählt inzwischen
unabhängig den UV-Kanal für diese Maps; UV1-Draws und MaterialAnim-Abnahme
bleiben als visuelle Laufzeitprüfung offen. Details:
[Retail-Licht-A/B](renderer-comparison-gallery.md#li_elite-retail-vmesh-unter-gerichteten-lichtern), [TBN-Kontrolle](renderer-comparison-gallery.md#tbn-achsen-und-y-kanal-gerichtete-shaderkontrolle), [NAP-Neutralmap-Lauf](renderer-comparison-gallery.md#retail-vmesh-mit-original--und-neutralmap-aus-nap) und [Retail-PBR-Maps](renderer-comparison-gallery.md#li_elite-retailmesh-roughness-und-metallic-getrennt).

Parser und Daten:

- [x] Schema `SurfaceMapsIni` in `LibreLancer.Data` mit Syntax-, Groß-/Kleinschreibungs-, Duplikat- und unbekannte-Schlüssel-Prüfungen umgesetzt; siehe `SurfaceMapsIniTests`.
- [x] `Freelancer.ini [Data]`-Ladeliste über `surface_maps` umgesetzt; fehlende optionale Surface-INIs erzeugen eine Diagnose und lassen das Basisspiel weiterladen.
- [x] Bibliotheken aus `[Texture]` werden bei passender Materialbindung über den `ResourceManager` aufgelöst. CPU-Bilder und GPU-Texturen laufen über getrennte Residency-Caches und Draw-Leases; die INI hält die Zuordnung statt alle Karten dauerhaft zu laden.

Material:

- [x] `Nm_name`-Metadaten werden nur bei passender `Dt_name`-Zuordnung ergänzt; ausdrückliche MAT-Maps behalten Vorrang.
- [x] Normalmap-`strength` wird als Materialparameter an den Shader weitergereicht; Roughness-/Metallic-Faktoren werden separat ausgewertet.
- [x] `Rt_name`/`Mt_name` aus der INI ergänzen und die UV-Menge festlegen: `GetTexCoord(3/4, …)` im Shader, `SetTextureCoordinates(…, MtFlags, RtFlags)`; MAT-Werte haben Vorrang.
- [ ] Metallische Flächen und Reflexionsstärke auf weiteren Assets prüfen; der geladene LI_ELITE-Produktionsdraw zeigt messbare, aber subtile Cubemap-Änderungen. GGX-IBL, visuelle Abnahme im Spiel und Materialtreue bleiben offen.
- [x] `[CubeMap] strength` aus dem expliziten Katalog als opt-in Materialparameter bis zum Basic-/PBR-Shader weiterreichen.
- [ ] Stärke und Cube-Orientierung auf Retail-Schiffen, Stationen und im Spiel visuell abnehmen.

Rendering:

- [ ] Tangentenraum für FVF-Modelle ohne Tangenten vollständig abdecken. Authored FVF-Tangenten sowie generierte VMesh-/DFM-Tangenten und QuadSphere sind vorhanden; verbleibende FVF-Layouts und UV-/Nahtfälle brauchen gezielte Abdeckung.
- [x] Ergänzende PBR-Maps aktivieren den PBR-Pfad unabhängig vom Normal-Map-Schalter. Normal Maps wirken bei vorhandener Tangentenbasis im PBR-Pfad; der Legacy-Shader bleibt kompatibel.
- [ ] Asset-Normalenpolarität (+Y oben) gegen eine unabhängige Retail-Referenz prüfen. Die Shaderachsen sind kontrolliert, die Polarität echter Karten ist noch nicht belegt.
- [x] Technische Filteranbindung für Surface-Maps: DDS-Mips werden erhalten, Material-Sampler übernehmen `PreferredFilterLevel`, und der OpenGL-Backend setzt Trilinearfilterung oder die gewählte Anisotropiestufe.
- [ ] Bildqualität mit Mips und Anisotropie auf Schimmern und Specular-Artefakten prüfen; Planeten-Detailkarten zusätzlich auf Kachelung vergleichen.

Prüfung:

- [x] Test: generierter Cube (`envcube_space.mat`) wird von `DDS.GetTextureCube` mit sechs Flächen und Mips gelesen; die Flächen-/Mip-Readbacks und geladenen LI_ELITE-Bilder stehen im Cubemap-Probebericht und der [Vergleichsgalerie](renderer-comparison-gallery.md).
- [x] DDS-Lader unterstützt zusätzlich DX10 `R16G16B16A16_FLOAT`-Cubemaps und reicht sie als lineare HalfVector4-Texturen an Basic- und PBR-Shader weiter. Vier headless Tests prüfen Cubemap/Mips, abgeschnittene Nutzdaten und nicht unterstützte Formate. Client und LancerEdit bauen; der visuelle ModelViewer-Nachweis mit dieser HDR-Cubemap fehlt weiterhin.
- [ ] Test: Die vier `normalmaps_*.txm` liefern zusammen 55 Texturen 1024 × 1024 im Format `Rgtc2` mit 9 Mips; Test der BC5-Dekodierung im `DDS`-Pfad (`ATI2N`).
- [ ] Sichttest mit einem Schiff (`li_fighter`), einer Station (`bw_pygar_airlock`) und einem Planeten mit Detailkarte; je mit und ohne Karte, Vergleich der Aufnahmen. Das Liberty-Stadtbild/full-Profil wurde nach dem Room-Lighting-Fix erneut verglichen; übrige Szenen und Materialtreue sind offen.
- [ ] Verhalten bei fehlender Datei, fehlender Karte und fehlerhaftem DDS (kein Absturz).
- [ ] Speicher und Ladezeit: 1,2 GB Normal-, Roughness- und Metallic-Karten (BC5/BC4), 3 × 2,1 MB Cube Maps.

Editor und Dokumentation:

- [x] LancerEdit zeigt Materialmaps und erlaubt ergänzende Surface-Map-Zuordnungen im Render-Tab in einer Workspace-INI zu bearbeiten.
- [x] Implementierungsstand und offene Abnahmen in dieser Dokumentation aktualisiert; Änderungen werden als Client-Patch in `patches/series` geführt.

## 6. Offene Entscheidungen

| Frage | Optionen |
|---|---|
| Wo steht die Zuordnung? | nur INI (hier beschrieben) oder zusätzlich in `.mat` (`Nm_name`, bereits möglich) |
| Welcher Shaderpfad? | Standard-Shader erweitern oder nur PBR (dann `Rt`/`Mt` oder Konstante nötig) |
| Tangenten | beim Laden erzeugen, im Shader ableiten oder Karten nur für Modelle mit Tangenten |
| Cube-Zuweisung | bei den Archetypen belassen oder automatisch je Systemtyp (Sonne, Nebel) |
