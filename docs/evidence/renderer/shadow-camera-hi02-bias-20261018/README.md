# Hi02 directional shadow bias A/B (2026-10-18)

## Runtime setup

- Release client, engine `03bfd87-git (20261017)`
- OpenGL 4.6, Mesa 23.2.1, Intel HD Graphics 4600 (HSW GT2)
- 1280x720, NAP-only application directory; no loose `DATA` directory
- Active manifest `lancer-renderer-ssao-hdr-20261018`; its 13 declared archive sizes and SHA-256 hashes were verified immediately before each launch. The package set was generated from `Data/NAP/`.
- The test save enters Hi02 with the `MP_ge_fighter` ship. The debug overlay was enabled to read the renderer state; the clean captures collapse that overlay after confirming the state.
- Normal/PBR maps, environment maps, HDR, bloom, SSAO and sun ambient were disabled to isolate legacy shading and directional shadows. The selected source was explicitly `Hi02_system_light`.

## Captures

| File | Setting | Evidence |
|---|---:|---|
| [Bias 0.0000](bias-0000.png) | `--render-shadow-bias=0` | Hi02 gameplay capture with the debug window collapsed |
| [Bias 0.0200](bias-0020.png) | `--render-shadow-bias=0.02` | Same save and setup, maximum accepted bias |
| [Side-by-side](bias-ab-side-by-side.png) | 0.0000 / 0.0200 | Resized comparison image |
| [Bias 0 debug](bias-0000-debug.png) | 0.0000 | Debug state reports active `Hi02_system_light` |
| [Bias 0.02 debug](bias-0020-debug.png) | 0.0200 | Debug state reports active `Hi02_system_light` |

Both debug states report `Shadow map: active 'Hi02_system_light' dir rgb=(0.910,0.314,0.000) Y=0.418`. This confirms the fit succeeds and the selected light reaches the live shadow-map path. The background ROI `(0,100)-(450,450)` is pixel-identical between captures. Raw 8-bit image comparison: full-frame mean absolute difference 0.452; ship ROI `(520,530)-(760,640)` mean 0.170; hull ROI `(570,545)-(710,625)` mean 0.384. These differences are subtle. The launches are separate and their reported camera positions vary by about 0.12 world units, so this is a useful visual comparison, not final bias calibration.

Bias calibration still needs same-session control if a live bias control is added, additional ship/base and alpha-test casters, shadow acne/peter-panning review, and representative GPU timing. No default change is justified by these two endpoints; the default remains 0.0015.
