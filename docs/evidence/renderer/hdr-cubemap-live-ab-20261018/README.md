# HDR cubemap live A/B — LI Elite (2026-10-18)

This same-session LancerEdit comparison verifies the HDR cubemap loader and the
PBR environment-reflection toggle against the LI Elite production mesh.

## Runtime and asset provenance

- Debug LancerEdit from the current Client overlay; SDL3, OpenGL 4.5, Mesa
  llvmpipe on XWayland.
- The application root was `/tmp/lancer-renderer-lit-20261018/app`. It had no
  loose `DATA` or `data` directory. Manifest `lancer-renderer-lit-20261018`
  referenced 13 NAP archives. Before launch, each archive's size and SHA-256
  were checked against `packages/active.json`; all matched. The active package
  set was built from `Data/NAP/` inputs.
- The `surface` archive contains the Surface assets from `Data/NAP/Surface/`.
  The selected file is `fx\envcube_sun.mat`.
- Its embedded DDS is 256×256, DX10 `R16G16B16A16_FLOAT`, with 9 mip levels.
  The sixth top-level cube face has RGB samples above 1 and a maximum of 3.5645,
  confirming that this is HDR data rather than merely a 16-bit LDR cube.
- Model: `Data/NAP/Models/ships/liberty/li_elite/li_elite.cmp`.
- ModelViewer was in `Lit` mode. `envcube_sun` was selected as the preview
  override and reported as a loaded `TextureCube`. Surface roughness/metallic
  maps and diffuse IBL were enabled; normal maps were disabled. The ambient
  swatch, camera, viewport, model, and all other renderer settings remained
  fixed. Only PBR environment reflections changed between captures.

## Captures and comparison

Both captures are 800×600 screenshots from the same viewer session:

| Image | PBR environment reflections |
|---|---|
| [Off](environment-off.png) | Disabled |
| [On](environment-on.png) | Enabled |

The model crop `(320,200)-(760,425)` contains 99,000 pixels. **13,492 pixels
differed** between the captures; normalized RMSE was `0.00182116`. A background
control crop `(610,80)-(770,180)` changed 0 pixels. The screenshot hashes are:

- Off: `ead1eae5039230cb43f2b5e6f7b4a4c5627cf44fb2c98a61474a808dc67828f`
- On: `34060747c5d6c544cef2249ce756c03475ed19ae39e127b447368c99dbff81ba`

This proves that the NAP HDR cube loads and that its reflection toggle affects
the production LI Elite draw. The difference is subtle. Retail color/orientation
calibration, other materials and in-game scenes, hardware-GPU performance, and
the separate minimize/restore framebuffer OOM remain open.
