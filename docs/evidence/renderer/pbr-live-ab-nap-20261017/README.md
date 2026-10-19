# LI Elite PBR live A/B with NAP-only assets — 2026-10-17

This same-process LancerEdit ModelViewer sequence checks Surface PBR maps,
PBR environment reflections and Surface normal maps on the LI Elite.

## Runtime and assets

- LancerEdit Release from the current Client overlay, version `03bfd87-git (20261017)`.
- OpenGL 4.6 / Mesa 23.2.1 / Intel HD Graphics 4600.
- The app root had no loose `DATA` directory. Its 13-package active snapshot was
  built from staged source directories under `Data/NAP/` and every archive
  passed `nexus-pack verify`. The active manifest ID was
  `lancer-renderer-current-20261017`.
- `EXE/freelancer.ini` and `EXE/dacom.ini` were copied from the staged Runtime
  inputs under `Data/NAP/`; game assets resolved through the active NAP mounts.
- Model: `Data/NAP/Models/ships/liberty/li_elite/li_elite.cmp`.
- Fixed ModelViewer camera, `Lit` mode, ambient color `#A7A6A6`, background on.
  No camera, model or ambient changes were made between captures.

## Captures

All images are 800x600 screenshots from the same viewer session.

| Image | Settings |
|---|---|
| [PBR off](pbr-off.png) | Surface PBR maps off, PBR environment reflections off, Surface normal maps off. |
| [Surface PBR maps](surface-pbr-maps.png) | Surface PBR maps on; environment and normal maps off. |
| [Environment option on](environment-on-default-cube.png) | Surface PBR maps and environment option on; normal maps off; archetype-default `envmapbasic` cube. |
| [Normal maps on](normal-maps-on.png) | Surface PBR maps, environment option and normal maps on. |

## Pixel comparison

The fixed model crop `(260,190)-(780,440)` contains 130,000 pixels. Between
PBR off and all three options on, 32,898 pixels changed (25.31%); normalized
RMSE was `0.0600`. A control crop in the upper right background changed 0 of
11,700 pixels.

Sequentially enabling only Surface PBR maps changed 32,907 model pixels
(normalized RMSE `0.0568`). Enabling the environment option with the default
`envmapbasic` cube changed 0 pixels. Enabling normal maps afterward changed
10,695 pixels (normalized RMSE `0.0288`).

The zero-difference environment step is expected for this material's
archetype-default `envmapbasic` cube: its six faces are neutral gray and it has
only one mip level. It does not show that environment reflections are broken.
A separate catalog-cube override test is required to judge the PBR environment
reflection path; earlier override evidence is linked from
[`pbr-world-reflection-20261017`](../pbr-world-reflection-20261017/README.md).

These captures establish that the current ModelViewer controls affect this
material's PBR and normal-map output. They do not establish retail color
calibration, in-game quality, or performance cost.

## Non-neutral cubemap A/B (2026-10-18)

A follow-up used the same LI Elite, camera, ambient, viewport, and active
`envcube_sun` preview override. Surface roughness/metallic maps were enabled;
the environment-reflection checkbox was the only change. Diffuse IBL and
normal maps remained enabled in both captures. The active NAP-only package set
and no-loose-`DATA` runtime are the same setup described above.

| Capture | Setting | SHA-256 |
|---|---|---|
| [Environment reflections on](environment-sun-on.png) | PBR environment reflections enabled | `acf964cafa853cff6bc4d13500ac2f25f404d3edc8e8dece61cb2be3f225dec3` |
| [Environment reflections off](environment-sun-off.png) | PBR environment reflections disabled | `fd0e9e45b65d4be3002aae20c3a6add2a104c47aeef04b74aef518f7f73c1d7f` |

In the model crop `(260,190)-(780,440)`, 15,597 pixels differed (12.00%);
normalized RMSE was `0.00179` and normalized MAE was `0.000303` (about
`0.077/255`). The upper-right background control crop was pixel-identical.
This confirms the toggle reaches the rendered PBR model with a non-neutral
catalog cube, but the average image response is subtle. Reflection strength,
materials and in-game scene quality still need calibration and fixed-scene
review; this is functional evidence, not visual-quality acceptance.
