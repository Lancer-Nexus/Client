# DFM PBR map-toggle A/B (2026-10-15)

A Release LancerEdit ModelViewer session loaded
`Data/NAP/Models/characters/bodies/li_male_elite_body.dfm`. The app's 13 NAP
archives were rebuilt from `Data/NAP/`, verified, and each archive entry's
length and SHA-256 matched its source file. The active snapshot loaded without
a loose `DATA` directory or `.gitkeep` entries. The viewer reported the Full
Surface profile (34 libraries, 2,807 normal-map entries).

The same DFM material and camera were captured with Surface roughness/metallic
maps off and on. Surface normal maps and PBR environment reflections stayed
off; generated tangents stayed on. The viewport is pixel-identical between the
captures, and a repeated OFF capture is also pixel-identical.

Inspection of LOD 0 `li_male_elite_1` (`DcDt`, diffuse `li_elite_male_1.tga`)
shows no Surface roughness map, metallic map, roughness factor, or metallic
factor. Therefore the toggle alone does not select a PBR material shader for
this DFM surface. This is a useful binding diagnosis, not a successful PBR
visual acceptance. Bind suitable DFM material factors/maps, then repeat the
comparison and collect timing data.

| Capture | Image |
|---|---|
| Surface PBR maps off | [pbr-maps-off-20261015.png](pbr-maps-off-20261015.png) |
| Surface PBR maps on | [pbr-maps-on-20261015.png](pbr-maps-on-20261015.png) |
| Machine-readable package and pixel evidence | [pbr-map-toggle-ab-20261015.json](pbr-map-toggle-ab-20261015.json) |

The viewport comparison used pixels `(282,56)-(1912,964)`: 0 changed pixels,
RMSE 0.0/255. OFF repeatability was also 0 changed pixels, RMSE 0.0/255. The
complete environment, package hashes, and SHA-256 values are recorded in the
JSON report.
