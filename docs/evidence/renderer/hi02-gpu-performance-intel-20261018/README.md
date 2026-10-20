# Hi02 GPU-Performanceprofil auf Intel HD 4600

Erfasst am 2026-10-18 mit dem Release-Client aus dem laufenden Client-Overlay. Szene: gespeicherter Spielstand Hi02, 1280×720, 39 sichtbare Objekte, OpenGL 4.6 auf Mesa Intel HD Graphics 4600 (Haswell GT2), Mesa 23.2.1. VSync aus; SMAA High, anisotrope Filterung 16x. Der Debug-Profiler war für diesen Lauf eingeschaltet.

Alle Renderoptionen aus [`profile.ini`](profile.ini) waren aktiviert: HDR, Bloom, Depth Prepass, Schatten, SSAO, Soft Particles, Partikel-/Beam-/Sprungtor-Effekte, kurzlebige Waffenlichter, Sun Ambient/Lens Glow, Surface-Profil Full mit Normal-/PBR-Maps, diffuse IBL und Legacy-Environment-Maps.

Der Lauf verwendete ausschließlich NAP-Archive; alle 13 Archive wurden vor dem Start gegen das aktive Manifest auf Größe und SHA-256 geprüft. Es gab kein loses DATA-Verzeichnis. Das Manifest ist in [`nap-active.json`](nap-active.json) enthalten; ID `lancer-softparticle-20261018`, SHA-256 `b00ed1225201d2f0a51c808b15565e1c3a8809c7cbd05e1fff2992453f9dc3de`.

## 300-Frame-Profil

Die vollständigen Rohdaten stehen in [`frame-profile.csv`](frame-profile.csv). CPU-Werte sind pro Frame; GPU-Passzeiten stammen aus asynchronen OpenGL-Timerabfragen und können einige Frames alt sein.

| Messwert | Median | p95 | p99 | Min | Max |
|---|---:|---:|---:|---:|---:|
| CPU update + draw | 5.80 ms | 7.59 ms | 8.75 ms | 4.41 ms | 9.58 ms |
| Frame interval | 10.06 ms | 12.31 ms | 12.68 ms | 9.27 ms | 15.74 ms |
| FPS | 99.44 FPS | 106.26 FPS | 107.29 FPS | 63.53 FPS | 107.90 FPS |

### Mittlere GPU-Passzeiten

| Pass | Mittelwert | Samples |
|---|---:|---:|
| transparent | 2.645 ms | 300 |
| aa resolve | 1.182 ms | 300 |
| ssao | 1.087 ms | 300 |
| smaa neighborhood blend | 0.776 ms | 300 |
| opaque | 0.752 ms | 300 |
| stars nebula | 0.715 ms | 300 |
| tone map | 0.631 ms | 300 |
| bloom extraction | 0.452 ms | 300 |
| smaa edge detection | 0.363 ms | 300 |
| depth | 0.280 ms | 300 |
| ssao depth prepass | 0.234 ms | 300 |
| bloom horizontal blur | 0.043 ms | 300 |

Die transparenten Objekte hatten mit rund 2,65 ms den höchsten mittleren Einzelpass. Das ist ein Messhinweis für weitere Profilierung, noch keine Optimierungsentscheidung: Die Szene, Auflösung und Hardware sind nur ein einzelner Lastfall. Der Screenshot [`profile-screenshot.png`](profile-screenshot.png) hält den aktiven Debug-Profiler und die Hi02-Szene fest.

## Aussagegrenzen und nächste Profile

- Der Debug-Profiler beeinflusst CPU-Zeit und FPS; die Werte sind keine saubere Endnutzer-Benchmark.
- Hi02 mit Nebel und 39 Objekten deckt weder Kampf, Intro/Cutscene, Planet, Asteroidenfeld noch große Stationsansichten ab.
- Das Profil misst keine Skalierung bei höheren Auflösungen, schwächerer Hardware oder Vulkan.
- Als nächste Schritte getrennte Profile mit Debug-Overlay aus, Soft Particles an/aus und repräsentativen Intro-, Kampf-, Planeten- und Asteroidenszenen erfassen; erst danach Budgets bzw. Optimierungsziele festlegen.
