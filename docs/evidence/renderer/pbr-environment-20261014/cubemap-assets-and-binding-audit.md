# PBR-Cubemap: Asset- und Bindungspfad-Audit

Stand: 2026-10-14. Dieses Audit verfolgt den vorhandenen `LI_ELITE`-PBR-Cubemap-
Pfad bis zu den NAP-Quellen. Der kontrollierte Produktionsshader- und MAT/DDS-
Probe ist inzwischen ergänzt; ein sichtbarer Effekt im echten LI_ELITE-Draw ist
weiterhin nicht bestätigt.

## Befund

- `BasicMaterial.Use()` wählt bei aktiviertem `PbrEnvironmentMapsEnabled` den
  Namen `Glass ? "envmapglass" : EnvMap` und löst ihn mit `GetTexture(5, name)`
  auf. `EnvMap` stammt aus `envmap_material` des Archetyps. Für `LI_ELITE` ist
  das `envmapbasic`; der PBR-Shader sampelt `TextureCube` an Register `t5`.
- Dieser Pfad fragt die `[CubeMap]`-Einträge aus der Surface-INI und die Karten
  `envcube_space`, `envcube_sun`, `envcube_nebula` nicht ab. Die Karten sind im
  NAP vorhanden und von `SurfaceMaps.ini` referenziert, aber sie werden über den
  aktuellen Archetyppfad nicht ausgewählt. Eine Auswahl anhand eines Surface-
  Profils oder einer Weltumgebung ist noch nicht implementiert.
- Der `CUBE`-DDS aus `Material/fx/envmapbasic.mat` ist 64x64, hat eine Mipstufe
  (im DDS-Header ist die rohe Mipzahl 0) und sechs Flächen. Die +X-Fläche ist
  im RGB-Mittelwert (127,127,127), wie auch vier weitere Flächen; +Y enthält
  den hellen Anteil. Ein direkter +X-Sample sollte daher kein schwarzes
  Quellpixel liefern. Das frühere schwarze Diagnosebild deutet weiter auf eine
  offene Shader-/Sampler-/GPU-Bindungs- oder Koordinatenfrage, nicht auf einen
  schwarzen +X-Assetinhalt.
- Die neuen `envcube_*`-Karten sind je 256x256 mit neun Mipstufen und sechs
  Flächen. Der Surface-Assetcheck hat 41 Dateien, 7005 DDS-Texturen und null
  Struktur-/Referenzfehler gemeldet. Das ist eine Datenintegritätsprüfung,
  keine visuelle oder Laufzeitabnahme.

## Nachprüfbare Quelldaten

Alle Laufzeitdaten unten stammen aus `Data/NAP/`; die Engine-Stellen stammen aus
`src/` des Client-Worktrees.

| NAP-Datei | SHA-256 | DDS | RGB-Mittelwert +X |
|---|---|---|---|
| `Material/fx/envmapbasic.mat` | `18c30f383fc451dcb5e596668770fcbc517cb88dadf7ea64c685a01c2a2432bc` | 64x64, 1 Mip, 6 Flächen | (127, 127, 127) |
| `Surface/fx/envcube_space.mat` | `c0b9908383e7e6837bf61d7c9f256636bf16cb455f800e3ebe9a10ebe3eba9db` | 256x256, 9 Mips, 6 Flächen | (9.86, 11.26, 21.95) |
| `Surface/fx/envcube_sun.mat` | `862be2bc062cf6d636c998b88c6fb9ca36434c7d5ff862544d0a78eba0b2b70f` | 256x256, 9 Mips, 6 Flächen | (18.06, 14.63, 11.48) |
| `Surface/fx/envcube_nebula.mat` | `c360f2222aea189f6bf0c62d37ff2ba2bd2d82c8bde4a14cb110abd0a30af0eb` | 256x256, 9 Mips, 6 Flächen | (39.43, 21.29, 45.22) |

Die +X-Statistik liest die DDS-Flächenfolge `+X, -X, +Y, -Y, +Z, -Z` und
interpretiert die gespeicherten 32-Bit-Kanäle als BGRA. Die Flächenfolge,
DDS-Größen und UTF-Knoten wurden aus den Dateien selbst gelesen.

| Engine-Datei | SHA-256 | Relevanter Pfad |
|---|---|---|
| `BasicMaterial.cs` | `bc4438ec45c780c102bfe8acca1773542cc46271ae10895bcb9f1baa62305cdf` | PBR wählt das Archetyp-`EnvMap` und bindet Cube an Slot 5 |
| `PBR.frag.hlsl` | `b56d7687b5a654c31fe9b9453e424adc3d9fbe006651ada67705f4aaf4deda6d` | `EnvTexture` ist `t5`; PBR berechnet Reflexionsrichtung und Mip |

## Ursprüngliche offene Schritte (vor GPU-Proben)

1. In einer RenderDoc-Aufnahme desselben `LI_ELITE`-Draws prüfen, ob Slot 5 eine
   nicht-schwarze `TextureCube`-Ressource mit sechs Flächen enthält und welcher
   Samplerzustand aktiv ist.
2. Einen synthetischen Cube mit sechs klar unterschiedlichen Farben über den
   Produktionsshader sampeln; dadurch lassen sich Face-Orientierung und
   Slot-/Samplerbindung auseinanderhalten.
3. Erst danach die `[CubeMap]`-Metadaten in den Laufzeit-Resolver integrieren,
   die Umgebungsauswahl festlegen und `strength` mit einem klaren Default
   anwenden. Die existierenden Oberflächenkarten bleiben dabei opt-in.

Diese Liste wurde durch die nachfolgenden GPU-Proben teilweise erledigt.

## Statische Slot- und Feature-Prüfung (2026-10-14)

Der weitere Quellabgleich findet keinen offensichtlichen Feature-Bit- oder
Slotversatz: `BasicMaterial.PBRFeatures.ENVMAP` ist Bit 6; `PBR.txt` ordnet
`ENVMAP` dem Featureindex 7 zu (einsbasierte Deklaration). `BasicMaterial.Use`
löst den Archetyp-Cube an Cache-/Texture-Slot 5 auf und bindet ihn bei aktivem
PBR-Umgebungsmodus an Slot 5. Der Fragmentshader deklariert `EnvTexture` als
`t5` und `EnvSampler` als `s5`. `TextureSlots` stellt acht Einträge bereit;
der OpenGL-Pfad wendet Sampler für die vom Shader reflektierten Textureinheiten
an. Damit ist die Zuordnung im C#- und Shader-Quelltext konsistent. Das beweist
weder die tatsächliche GPU-Ressource im LI_ELITE-Draw noch den Samplewert und
ersetzt nicht den RenderDoc- oder synthetischen Farbflächen-Probe.

## Synthetischer OpenGL-Probe (2026-10-14)

Der zuvor offene Farbflächen-Probe ist jetzt mit SDL3s Offscreen-Treiber auf
Intel HD Graphics 4600 / Mesa 23.2 gelaufen. Ein eigener HLSL-Testshader mit
`TextureCube` an `t5` und `SamplerState` an `s5` hat alle sechs Richtungen aus
einem künstlichen Cube korrekt zurückgelesen. Die Samples entsprechen den
jeweils hochgeladenen Farben für +X, -X, +Y, -Y, +Z und -Z. Bild und Messwerte
stehen in [synthetic-cubemap-slot5-20261014.md](synthetic-cubemap-slot5-20261014.md).
Damit sind generischer GL-Cube-Upload, Slot 5, Sample und RenderTarget-Readback
auf diesem Gerät bestätigt. Das ist kein Lauf des produktiven PBR-Fragments
und kein Test des `envmapbasic`-DDS-Uploads.

Außerdem war der Regressionstest für den C#-PBR-Uniform-Block veraltet: Er
erwartete 48 statt 64 Byte, obwohl `EnvMipCount` bei Offset 48 ergänzt wurde.
Die Erwartung ist im Patch `2009-client-regression-tests.patch` auf 64 Byte
korrigiert; ein zusätzlicher Test prüft `EnvMipCount` an Offset 48. Die fokussierte
Suite besteht nun 8/8. Die Patchserie rekonstruiert anschließend 530 Dateien.

Der noch offene Laufzeitpunkt ist die unveränderte LI_ELITE-ModelViewer-A/B:
deren ROI bleibt bei null Bildänderung. Die kontrollierten Tests unten grenzen
das Problem auf die echte Material-/Mesh-/Draw-Konfiguration oder die zu geringe
sichtbare Reflexionsstärke ein.

## Kontrollierter Produktions-PBR- und MAT/DDS-Probe (2026-10-14)

Ein isolierter SDL3-Offscreen-Draw verwendete den eingebetteten
`PBR.bin`-Shaderbundle aus dem gebauten Client, die `ENVMAP`-Variante (Feature
64), einen einfachen FVF-Dreiecksdraw und denselben Slot `t5/s5`. Die Cube-Maps
wurden nicht künstlich erzeugt: `UtfLoader.LoadResourceFile` las MAT-Dateien
aus `Data/NAP/`, `TextureData.Initialize` führte den eingebetteten CUBE-DDS
durch `DDS.FromStream`, und der erhaltene `TextureCube` wurde direkt dem
Produktions-PBR-Shader übergeben.

Die vier MATs wurden erfolgreich geladen. `envmapbasic` hat eine Mipstufe;
`envcube_space`, `envcube_sun` und `envcube_nebula` haben jeweils neun. Für die
drei 9-Mip-Cubes wurde mit Roughness 0,25 Mip-Level 2 abgetastet. Kamera,
Geometrie, Weiß-Diffuse, Metallic 1, Licht aus und die Reflexionsrichtung waren
für alle vier Läufe identisch. Die gemessenen RGB-Mittelwerte des Mittelpixels:

| Cube aus NAP | Mips | RGB-Readback |
|---|---:|---:|
| `Material/fx/envmapbasic.mat` | 1 | (126, 126, 126) |
| `Surface/fx/envcube_space.mat` | 9 | (13, 14, 22) |
| `Surface/fx/envcube_sun.mat` | 9 | (17, 17, 21) |
| `Surface/fx/envcube_nebula.mat` | 9 | (23, 18, 25) |

![Produktions-PBR-Shader mit den vier Cube-MATs](production-pbr-mat-cubemaps-20261014.png)

Das bestätigt auf Intel HD Graphics 4600 / Mesa 23.2 den tatsächlichen
Produktions-PBR-Shader, seine ENVMAP-Variante, Cube-Slotbindung und den
MAT→UTF-Texture-Library→DDS→TextureCube-Ladepfad einschließlich Mip-Sampling.
Die Quell-Hashes stimmen mit den oben dokumentierten NAP-/Shaderdateien
überein. Die ausführbare Probe und ihr Build liegen temporär unter
`/tmp/lancer-cubemap-gpu-probe/`; die Laufzeitinputs stammen ausschließlich
aus `Data/NAP/`.

Das beweist noch keine korrekte Reflexion auf einem Retail-Schiff: Der Probe
verwendet eine einfache Testgeometrie und kontrollierte Kamerauniformen. Die
LI_ELITE-A/B-ROI bleibt unverändert. Cube-Orientierung auf Retail-Meshes,
Reflexionsstärke, Auswahl anhand von Umgebung/System sowie `[CubeMap] strength`
und eine GGX-prefiltered IBL bleiben offen.
