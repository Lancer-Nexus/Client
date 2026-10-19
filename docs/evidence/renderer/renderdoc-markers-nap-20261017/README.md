# RenderDoc pass markers: NAP-only runtime smoke (2026-10-17)

## Setup and provenance

- Release client binary used the current `LibreLancer.dll` build.
- Active package manifest: `lancer-diffuse-ibl-20261018`, 14 packages. Every
  archive matched the manifest size and SHA-256 and passed `nexus-pack verify`.
  The base package input snapshot was
  `Data/NAP/.runtime-test-stage/package-inputs-20261016/`; the delta surface
  package inputs are under
  `Data/NAP/.runtime-test-stage/diffuse-ibl-20261018/`.
- The runtime application had no loose root `DATA` directory.
- Renderer: OpenGL 4.6 Core, Mesa 23.2.1, Intel HD Graphics 4600.
- Launch overrides: `LL_RENDERDOC_MATERIAL_MARKERS=1`,
  `--new-game --render-shadows=on`; isolated XDG config/state/data directories.

## Result

The GL log reported `RenderDoc material debug groups enabled` at 08:28:52 local
time. The same process finished loading game data, loaded the UI, and entered
`FP7_system`. It remained running until an external 45-second timeout terminated
it (shell status 124). Neither the captured stdout nor the application log
contained a crash or GL error.

This exercises marker-enabled rendering through the M01A system path on the
tested OpenGL driver. The source binds marker names to each
`SystemRenderPasses` descriptor and its subpasses. No RenderDoc capture file was
recorded, so this does not independently show the marker tree inside RenderDoc.
Other drivers and backends remain unverified.

The isolated logs are outside the repository at
`/tmp/lancer-renderdoc-markers-20261018/client.stdout.log` and
`/tmp/lancer-renderdoc-markers-20261018/state/Librelancer/logs/lancer.log.txt`;
both have SHA-256
`49d3310abeba69cb431ace0a9ea07eeb5fc6557cee262a67320414aa5ba6730`.
