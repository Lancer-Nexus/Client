# PBR cubemap selection runtime A/B (2026-10-17)

This is a same-process LancerEdit ModelViewer comparison of `LI_ELITE.cmp`.
All frames use the same model, camera pose, `Lit` mode, grey ambient value
(0.491/0.487/0.487), and enabled Surface roughness/metallic maps and PBR
environment reflections. The selected material's cubemap preview is the only
changed render input. The UI reports `Environment texture: TextureCube` for all
three catalog entries.

| Preview cubemap | Model viewport changed pixels vs archetype default | RMSE |
|---|---:|---:|
| `envcube_space` | 27,903 | 0.00145937 |
| `envcube_sun` | 25,340 | 0.00117693 |
| `envcube_nebula` | 27,196 | 0.00136616 |

Pixel counts use ImageMagick `compare -metric AE` over the rendered viewport
crop `540x500+260+60`, excluding the Render controls and status bar. RMSE is
normalized by the 8-bit channel range. Each image is 800x600.

| Image | Cubemap | SHA-256 |
|---|---|---|
| [archetype-default.png](archetype-default.png) | Archetype `envmapbasic` | `d23edc751adae4c7be7be036ef3722f3a75478381b0c5b7018a42b1f35bb133f` |
| [envcube-space.png](envcube-space.png) | `envcube_space` | `ed59cab34d5b1b42ecdad7ac621f4fd170ac28061193a85e433eecbb76012a48` |
| [envcube-sun.png](envcube-sun.png) | `envcube_sun` | `50679d81876d106b09ab57f1bdb160d5d6e6205890cf0aa2c5efc823cb8d270f` |
| [envcube-nebula.png](envcube-nebula.png) | `envcube_nebula` | `025d6808c9c07b1ddba9ddec5ec38d4288929acac76918e0d171f13d12b5c4e1` |

## Runtime and data provenance

- LancerEdit Release build: 0 errors, 213 warnings.
- OpenGL 4.6, Mesa 23.2.1, Intel HD Graphics 4600.
- NAP-only application: `/tmp/lancer-shadow-diagnostic-run-20261017`, with no root `DATA` directory.
- Active manifest: `lancer-renderer-aa-profile-20261016`, SHA-256 `e6d56a0f74095c89712f35f35586bfbe5db19d28f0f106654037a53140fc3ca1`.
- All 13 package sizes and SHA-256 values matched `packages/active.json`; all 13 archives passed `nexus-pack verify`.
- `package-inputs.txt` names only class directories under `Data/NAP/.runtime-test-stage/package-inputs-20261016/` and the runtime directory under `Data/NAP/QA/runtime-smoke-20261014/`.
- LancerEdit reached `Game: Finished loading game data in 1.627 seconds`.

This proves the ModelViewer preview selection loads and changes PBR output for
these NAP assets. It does not establish correct retail cubemap orientation,
material calibration, in-game behavior, or performance. GGX-prefiltered
radiance and convolved diffuse irradiance remain open.
