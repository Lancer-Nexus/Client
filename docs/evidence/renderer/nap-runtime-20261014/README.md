# NAP runtime smoke and renderer captures (2026-10-14)

The test application used 12 class packages built from `Data/NAP/{Effects,Equipment,Material,Mission,Models,Script,Sounds,Surface,System,UI,Video,World}` and one runtime package built from `Data/NAP/QA/runtime-smoke-20261014`. All 13 NAP archives passed `nexus-pack verify`. The active snapshot hashes and sizes were checked, then `NexusPackageFileProvider.LoadActive()` mounted all packages without path conflicts. `EXE/newplayer.fl` and `fx/SurfaceMaps.ini` resolved; no root `.gitkeep` was visible.

The Release Client ran with no loose `DATA` directory. Its log reached `Finished loading game data`, `Interface loaded`, `Client: Spawning in FP7_system`, and `Client: Found thorn mainObject`. It ran for 70 seconds until the harness timeout (exit 124), with no crash. The test used an Intel HD Graphics 4600 / Mesa 23.2 OpenGL 4.6 renderer.

- `environment-on-intro.png`: `--render-pbr-environment-maps=on`
- `environment-off-intro.png`: `--render-pbr-environment-maps=off`
- Both runs also enabled normal maps, surface PBR maps, and generated tangents.

These are captures from the animated M01A intro. The camera and scene timing differ between runs, so they document the two runtime paths but do not prove a visual cubemap difference. A fixed-camera ModelViewer or RenderDoc capture is still needed to verify the cubemap resource, sampled face, and visible reflection.

The run logged unsupported or duplicate INI entries, unimplemented mission actions, occasional simulation tick drops, slow update warnings, and a sun `.sph` warning. Their causes and performance impact are still open. Raw logs and NAP archives remain in `/tmp/lancer-nap-regression-20261014`; this evidence folder contains only screenshots and this summary and is intentionally left untracked.

## Static model-viewer attempt

A Release LancerEdit instance used a temporary app directory whose 13 package files were hard links to the verified `Data/NAP`-built archives. `--data` pointed at the NAP-only Client application directory. The editor loaded `li_elite.cmp` and logged `Finished loading game data in 1.518 seconds`. After clicking **View Model**, the ModelViewer image capture and X11 queries stopped responding; LancerEdit remained alive until the 120-second timeout and exited with 124. Its log contains no exception or crash report. X11 queries also remained unresponsive after LancerEdit exited, so no fixed-camera image or environment-toggle A/B was captured. The ModelViewer render state, cubemap selection, and the cause of the X11 stall remain unverified.

## SDL offscreen editor start

On 2026-10-14, LancerEdit was started with `SDL_VIDEODRIVER=offscreen` against
the same NAP-only app directory. SDL created an OpenGL 4.6 context on the Intel
HD Graphics 4600 / Mesa 23.2 driver despite the unresponsive X11 display. The
editor loaded all Freelancer game data in 2.265 seconds and emitted no crash or
exception in the captured log. The 25-second harness timeout stopped the still
running editor. This confirms an offscreen startup path, but it did not open
the ModelViewer or capture an image; cubemap face sampling and visible
reflections remain unverified. The active manifest and 13 archives remain the
same verified set described above, with no loose `DATA` directory.

## Sun lens-glow runtime A/B (2026-10-14)

The Release client was rebuilt with patch 2064 active. All 13 active NAP
archives matched `active.json` by size and SHA-256, passed `nexus-pack verify`,
and were mounted with no loose `DATA` directory. The isolated test root
contained only `EXE/freelancer.ini` and `EXE/dacom.ini` from the matching
`Data/SDK/exe` configuration; all game assets came from the verified NAP
snapshot. Both runs used `--new-game`; logs reached game-data load, interface
load, FP7 spawn, and the M01A `mainObject`. Both were then stopped after capture.

| Setting | Capture |
| --- | --- |
| `--render-sun-lens-glow=on` | `lens-glow-on.png` |
| `--render-sun-lens-glow=off` | `lens-glow-off.png` |

`lens-glow-ab.png` and `lens-glow-detail-ab.png` place the current captures
side by side. The scene is animated and the camera differs slightly between
runs; the visible star is partly occluded by the foreground silhouette. Both
runs reached the same startup milestones without exceptions or crash entries,
but each dropped stale simulation ticks during startup (24 with glow on, 23
with glow off). The captures confirm both option paths render; they do not
isolate the glow visually or prove retail parity.

The initial in-world run exposed a renderer crash: a cubemap resolved in a 2D
material sampler was cast to `Texture2D` during drawing. Patch
`2063-safe-2d-texture-binding.patch` makes 2D sampler binding type-safe and
falls back to the null texture for non-2D resources. The rebuilt client
completed both in-world runs without that exception. Patch 2064 passes the
`glow_fade_in_seconds` / `glow_fade_out_seconds` values to `SunRenderer` and
ramps lens-glow alpha when the sun enters or leaves the camera frustum. The
captures above use patch 2064. This first pass does not account for geometry
occlusion; visual fade timing, sun visibility transitions, and comparison with
retail Freelancer remain open because the animated capture is not a controlled
camera sequence.

## Lens-glow option startup smoke test (2026-10-15)

The test was repeated against the active 13-package snapshot after verifying
each archive's `active.json` size and SHA-256, `nexus-pack verify`, and every
archive file against its source under `Data/NAP/`. The client ran without a
loose `DATA` directory. Both `--render-sun-lens-glow=on` and `=off` reached
`Finished loading game data`, `Interface loaded`, `Spawning in FP7_system`,
and `Found thorn mainObject`, with no exception or crash entry.

- `lens-glow-on-20261015.png`
- `lens-glow-off-20261015.png`
- `lens-glow-ab-20261015.png` (labeled side-by-side overview)

These images are separate animated-intro captures and show substantially
different camera positions and scene lighting. They only demonstrate that
both option values start and render; they are not a controlled visual A/B and
do not establish lens-glow behavior or retail parity. The startup logs also
report 25 dropped stale simulation ticks in the ON run and 26 in the OFF run,
followed by occasional single-tick drops. Treat performance as unresolved.
