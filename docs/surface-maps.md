# Surface Maps: Normal Maps und Cube Maps per INI

**Status:** Beschreibung und Aufgabenliste, noch nichts im Client umgesetzt. **Stand:** 2026-10-12.
**Datenseite:** Repository `LN-Data` (`tools/make_surface_maps.py`, `SDK/fx/SurfaceMaps.ini`, `TODO-surface-maps.md`).
Engine-Änderungen bleiben gemäß `AGENTS.md` patch-only unter `patches/series`.

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
roughness_map = rt_li_elite_hull ; optional, Einkanalkarte (R) in derselben Bibliothek
roughness     = 1                ; Faktor auf die Karte
metallic_map  = mt_li_elite_hull ; optional
metallic      = 1                ; Faktor; ohne Karte 0 (Engine-Standard wäre 1)

[CubeMap]
nickname = envcube_space         ; Knoten in der .mat = envmap_material
file     = fx\envcube_space.mat  ; = material_library
kind     = space                 ; space | sun | nebula (Hinweis)
strength = 0.35                  ; Vorschlag für die Stärke der Spiegelung (0..1)
```

Regeln für die Auflösung (Vorschlag):

1. `[NormalMap]` gilt für jedes Material, dessen `Dt_name` gleich `texture` ist. Ein vorhandenes `Nm_name`, `Rt_name` oder `Mt_name` im `.mat` hat Vorrang, die INI füllt nur Lücken. Mit `roughness_map` oder `metallic_map` wird der PBR-Pfad des Materials benutzt.
2. Mehrere `[NormalMap]` für dieselbe Textur: der letzte Eintrag gewinnt, der Rest wird gewarnt.
3. Fehlt die Bibliothek oder die Karte, bleibt das Material unverändert (Warnung im Log, kein Fehler).
4. `[CubeMap]` ist ein **Katalog**. Gebunden wird weiter über `material_library` und `envmap_material` in `ships\Shiparch.ini` und `solar\SolarArch.ini`.
5. Der Original-Client ignoriert die Datei, solange sie nicht in der `Freelancer.ini` verlinkt ist.

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

Parser und Daten:

- [ ] Schema `SurfaceMapsIni` in `LibreLancer.Data` (Abschnitte `Texture`, `NormalMap`, `CubeMap`) mit Parsertests (Syntax aus 3, Groß-/Kleinschreibung, doppelte Einträge, unbekannte Schlüssel).
- [ ] Ladeliste: Schlüssel in `Freelancer.ini [Data]` (Vorschlag `surface_maps`) oder feste Suche nach `fx\SurfaceMaps.ini`; Verhalten ohne Datei unverändert.
- [ ] Bibliothek aus `[Texture]` über den `ResourceManager` laden (wie andere `.txm`), Karten nach Namen auflösen. Bei den 30 Bibliotheken aus `SurfaceMapsShips.ini`, `SurfaceMapsSolar.ini` und `SurfaceMapsBases.ini` (bis 50 MB, zusammen 0,95 GB) nur bei Bedarf laden (erst wenn eine passende Diffuse-Textur gebunden wird) und wieder freigeben; Index aus der INI (Textur → Bibliothek) im Speicher halten, nicht die Texturen.

Material:

- [ ] In `Material.Initialize` (oder davor) `Nm_name` aus der INI ergänzen, wenn `Dt_name` passt und `Nm_name` leer ist. Regeln aus 3.
- [ ] `strength` als Material-Parameter (Shader-Konstante) einführen oder ablehnen und die Karte vorskalieren. Roughness und Metallic haben bereits Faktoren (`PBRParameters.Roughness`/`Metallic`).
- [ ] `Rt_name`/`Mt_name` aus der INI ergänzen und die UV-Menge festlegen: `GetTexCoord(3/4, …)` im Shader, `SetTextureCoordinates(…, MtFlags, RtFlags)`; Freelancer-Modelle haben meist nur ein bis zwei UV-Mengen.
- [ ] Metallische Flächen ohne Umgebungsreflexion sind dunkel (`diffuseColor *= 1 - metallic`). Entscheiden: IBL/`envmap_material` im PBR-Pfad nutzen oder `metallic` in den INIs begrenzen.
- [ ] Reflexionsstärke für `[CubeMap]` entscheiden (neue Konstante oder nicht unterstützt).

Rendering:

- [ ] Tangentenraum für FVF-Modelle ohne Tangenten: Erzeugung beim Laden (aus Position und UV) oder im Shader über Ableitungen; Auswirkung auf Skinned/DFM-Modelle.
- [ ] Entscheiden, ob Normal Maps im Standard-Shader (Vertexbeleuchtung) oder nur im PBR-Pfad wirken; `pbr`-Bedingung in `BasicMaterial.Use` entsprechend.
- [ ] Tangentenraum der Daten (+Y oben) gegen den Shader prüfen. Bei invertiertem Grün das Vorzeichen in der Engine anpassen, nicht in den Daten.
- [ ] Mip-Filter und Anisotropie für die Karten prüfen (Planeten-Detailkarten sind kachelnd).

Prüfung:

- [ ] Test: generierter Cube (`envcube_space.mat`) wird von `DDS.GetTextureCube` mit sechs Flächen und Mips gelesen.
- [ ] Test: Die vier `normalmaps_*.txm` liefern zusammen 55 Texturen 1024 × 1024 im Format `Rgtc2` mit 9 Mips; Test der BC5-Dekodierung im `DDS`-Pfad (`ATI2N`).
- [ ] Sichttest mit einem Schiff (`li_fighter`), einer Station (`bw_pygar_airlock`) und einem Planeten mit Detailkarte; je mit und ohne Karte, Vergleich der Aufnahmen.
- [ ] Verhalten bei fehlender Datei, fehlender Karte und fehlerhaftem DDS (kein Absturz).
- [ ] Speicher und Ladezeit: 1,2 GB Normal-, Roughness- und Metallic-Karten (BC5/BC4), 3 × 2,1 MB Cube Maps.

Editor und Dokumentation:

- [ ] LancerEdit: Normal Map eines Materials anzeigen/setzen, optional die INI schreiben.
- [ ] README und diese Datei nach der Umsetzung auf den tatsächlichen Stand bringen; Patch in `patches/series` eintragen.

## 6. Offene Entscheidungen

| Frage | Optionen |
|---|---|
| Wo steht die Zuordnung? | nur INI (hier beschrieben) oder zusätzlich in `.mat` (`Nm_name`, bereits möglich) |
| Welcher Shaderpfad? | Standard-Shader erweitern oder nur PBR (dann `Rt`/`Mt` oder Konstante nötig) |
| Tangenten | beim Laden erzeugen, im Shader ableiten oder Karten nur für Modelle mit Tangenten |
| Cube-Zuweisung | bei den Archetypen belassen oder automatisch je Systemtyp (Sonne, Nebel) |
