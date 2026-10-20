# PBR Cubemap reflection direction: runtime check (2026-10-17)

LancerEdit Release ModelViewer loaded `LI_ELITE.cmp` from the NAP-only runtime
root `/tmp/lancer-shadow-diagnostic-run-20261017`. The preview used Lit mode,
Surface roughness/metallic maps and PBR environment reflections enabled, plus a
neutral gray ambient color (`#BEBABA`). The selected cubemap was the only
intended per-image render input. Each image is 800x600; the pixel comparison
uses viewport crop `517x490+283+60`, excluding the side panel and status bar.

| Cubemap | Changed viewport pixels vs archetype default (AE) | Normalized RMSE |
|---|---:|---:|
| `envcube_space` | 15,245 | 0.00117565 |
| `envcube_sun` | 17,729 | 0.00149511 |
| `envcube_nebula` | 15,229 | 0.00113934 |

The PBR shader now computes the lookup from its world-space normal and view
vector, while retaining the existing DDS Z-axis flip. These captures show that
the selected catalog cubes affect the current preview. They do not establish
that the reflection direction matches retail Freelancer, that the material
response is calibrated, or that the difference is visually adequate in normal
gameplay. GGX-prefiltered radiance and convolved diffuse irradiance remain
open.

## Runtime details

- Release LancerEdit build: 0 warnings, 0 errors.
- OpenGL 4.6 / Mesa 23.2.1 / Intel HD Graphics 4600.
- The process log records `Finished loading game data in 1.939 seconds` and no
  `Unable to load CubeMap` or `did not resolve to a TextureCube` message.
- Active NAP manifest: `lancer-renderer-aa-profile-20261016`, SHA-256
  `e6d56a0f74095c89712f35f35586bfbe5db19d28f0f106654037a53140fc3ca1`.
  All 13 package archives were verified against `packages/active.json` before
  launch; archive inputs were under `Data/NAP/` only, and the runtime root had
  no loose root `DATA` directory.

## Images and hashes

| Image | SHA-256 |
|---|---|
| `archetype-default.png` | `f4c60eb7d66f730c80343b383d843178c77308cbbb60bef83cd9596b24c108f5` |
| `envcube-space.png` | `23dc0031d33e1dcc2ec9c13ba39c67f35af5f904ec9caa02f312fa4d36967dc9` |
| `envcube-sun.png` | `815d96d64f1b3e80ecb40fc1ae20e63c0674aba0775b54d9f6eb5d400b10f2ee` |
| `envcube-nebula.png` | `a478df21668a635db3bf1011c4685908825d7b8060ee5e863ee548d9f3d7ec7e` |
