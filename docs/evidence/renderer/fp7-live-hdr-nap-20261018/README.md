# FP7 live scene with HDR render options enabled (2026-10-18)

A new-game run reached the FP7 system, spawned the intro scene and rendered a
paused in-game frame with the optional renderer settings enabled. This records
a positive runtime load/render check; it is not an HDR on/off comparison or a
quality acceptance.

## Runtime provenance

- Client built from the current Client overlay, version `03bfd87-git (20261017)`.
- OpenGL 4.5, Mesa 23.2.1 llvmpipe, running on an isolated Xephyr display at
  1280×720. The desktop display was not used for the game window.
- Active manifest `lancer-renderer-pygar-map-medium-20261018` contains 14 NAP
  archives. Their sizes and SHA-256 hashes were checked against
  `packages/active.json` before launch; all matched. A later structural
  `nexus-pack verify` passed for all 14 archives. The app root contained no
  loose `DATA` or `data` directory. The manifest's NAP inputs are documented as built from
  `Data/NAP/`.
- Configuration enabled HDR, bloom, shadows, SSAO, sun ambient, sun lens glow,
  soft particles, normal maps, surface PBR maps, PBR environment maps, diffuse
  IBL and legacy environment maps. Startup also explicitly passed
  `--render-hdr=on --render-bloom=on --render-pbr-environment-maps=on`.
- Logs show `Finished loading game data`, `Interface loaded`,
  `Spawning in FP7_system` and `Entering system FP7_system`. No exception or
  error occurred during this run. Existing Data warnings about unknown INI
  entries are unrelated to the rendering capture and remain visible in the log.
- F1 paused the scene before capture to reduce motion.

## Capture

[Paused FP7 scene](fp7-paused-hdr-nap-live-20261018.png) shows the rendered
station and nebula around the pause overlay. SHA-256:

`c33ecd42bad843e0e3940b3761367017b69691c6363f16bb6eca8d1f87ec4c02`

The scene is visibly rendered with these settings, but this single frame does
not prove that every enabled option contributed to the pixels. Rows 0–100 and 620–719 are uniformly black. The running-THN path deliberately
letterboxes to `height = windowHeight / 1.39`; at 1280×720 this yields a 518 px
viewport with 101 px bars. The measured black rows match those bars. The scene
content itself is naturally dark in parts, so this capture does not indicate a
viewport defect. The relevant code is `SpaceGameplay.Draw` at lines 2219–2231. The pause panel covers the center of the view. A fixed-state HDR/LDR
comparison, unobscured captures, retail-client reference and hardware-GPU
repeat remain necessary.
