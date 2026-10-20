# FP7 intro renderer feature matrix — 2026-10-17

Visual smoke captures from the same Linux/NAP runtime and the early FP7 intro
camera. They are near the same scene time (about 5.6–5.9 s). The active
`lancer-pbr-lighting-qa-20261017` manifest was checked before launch: 13 NAP
archives, declared sizes and SHA256 values matched, and the install had no
loose root `DATA` directory.

| Capture | Render options |
|---|---|
| `default.png` | Default renderer settings; debug overlay enabled for pass and frame data. |
| `surface-maps-off.png` | Full post/light options, with Normal Maps, PBR maps, environment maps and generated tangents explicitly off. |
| `all-options-on.png` | Surface profile Full; normal/PBR/environment maps, generated tangents, per-pixel and dynamic lighting, HDR, Bloom, depth prepass, shadows, SSAO, soft particles, SMAA High and anisotropy 16 enabled. |

All three captures show blue polygon patches on the station. They are visible in
the legacy default capture and remain when surface maps are explicitly disabled;
they also remain in additional captures with shadows/depth prepass disabled and
with HDR disabled. This does not identify their source or establish whether they
match retail Freelancer. A same-camera retail comparison and material/mesh trace
remain open before changing the legacy path.

The all-options run reached `Game: Finished loading game data` and
`UI: Interface loaded`, entered the intro, and ran for more than 30 seconds
without a RenderGraph exception. The debug overlay reported OpenGL 4.6 on Mesa
23.2.1 / Intel HD Graphics 4600, 36.38 FPS at capture, frame interval
p50/p95/p99 24.98/32.51/372.41 ms, and asynchronous GPU samples including
Opaque 5.081 ms, Transparent 1.762 ms, SMAA resolve 3.318 ms, SSAO 1.480 ms,
Bloom extraction 0.685 ms and tone map 1.017 ms. These are startup-scene samples
on one older integrated GPU, not a representative performance baseline.

The debug panels are part of the screenshots and cover portions of the scene.
