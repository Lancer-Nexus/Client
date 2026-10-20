# PBR texture-slot and map-binding check — 2026-10-18

## Fix

The OpenGL binding cache allocated eight texture units, while
`TextureSlots.Capacity` is nine and `PBR.frag.hlsl` declares `BtTexture` at
unit 8. `GLBind` now allocates its cache from the shared capacity. The existing
GL render-context guards report invalid shader, sampler, stencil, or texture
state indices with descriptive exceptions. A regression test asserts that the
cache size matches `TextureSlots.Capacity` and includes unit 8.

## Runtime setup

LancerEdit Debug ran on OpenGL / Mesa llvmpipe at 800x600. The app root had no
loose `DATA` directory. All 13 NAP archives were built from directories under
`Data/NAP/`, matched the active manifest sizes and SHA-256 values, and passed
`nexus-pack verify`. The active surface package was version 1. The DFM QA map
entry was enabled in the copied `EXE/freelancer.ini`; its map and texture inputs
were already in `Data/NAP/Surface/` before packaging.

## Results

- The LI Elite resolves `li_playerships.mat` and `envmapbasic.mat`. The ModelViewer
  Surface panel reports the loaded surface catalog and material assignments.
- In `Lit` mode, toggling only “Surface roughness/metallic maps” changed 3,290
  pixels in the fixed ship viewport crop (`450x240+320+195`); normalized RMSE
  was `0.0599678`. This confirms the map assignment affects the PBR render path.
- The DFM QA toggle also remains enabled without the former GL texture-slot
  index exception. That DFM has no matching ship or solar archetype, so it is
  only a crash-path check, not a visual PBR comparison.
- An earlier Xvfb run terminated after about two minutes with Mesa reporting
  `GL_OUT_OF_MEMORY in Resizing framebuffer` during `GL.Clear`. The current
  X11 desktop run remained alive for over seven minutes, including resizes to
  1280x720 and 1920x995 and a minimize/restore cycle, without that error. The
  earlier failure was not reproduced; a Linux software-renderer run does not
  replace longer hardware and platform stability profiles. Focused PBR, normal-map, and slot-capacity tests
passed 12/12.

## Captures

All captures are from the same ModelViewer process. `Lit` mode, model, camera,
ambient setting and background stayed fixed within each pair.

| Capture | State |
|---|---|
| [DFM maps off](dfm-pbr-maps-off.png) | Surface PBR maps disabled. |
| [DFM maps on](dfm-pbr-maps-on.png) | DFM QA Surface PBR maps enabled. |
| [LI Elite maps off](li-elite-pbr-maps-off.png) | Surface roughness/metallic maps disabled. |
| [LI Elite maps on](li-elite-pbr-maps-on.png) | Surface roughness/metallic maps enabled; visible ship-surface change. |
