# Hi02 HDR/LDR live A/B (2026-10-18)

The two gameplay captures were taken in one client process from the same loaded
Hi02 save. HDR tone mapping was enabled at launch, then switched off in the
in-game Effects page while the game was paused. The option screenshots document
the checked and unchecked states. This keeps the save and camera in one process, avoiding the large scene drift
found in the earlier two-process attempt.

## Runtime and inputs

- Client: current Release app under `/tmp/lancer-renderer-nebula-20261018/app-current`,
  engine version `03bfd87-git (20261017)`.
- OpenGL 4.5 / Mesa llvmpipe in isolated Xephyr at 1280×720; .NET 10 from
  `/opt/dotnet-10`.
- Save: `/tmp/lancer-shadow-bias-ab/hi02-space-test.fl` (Hi02, ge_fighter).
- NAP-only application root; no loose `DATA` or `data` directory.
- Active manifest `lancer-nebula-ambient-smoke-20261018`, SHA-256
  `ba8f9b763c251e99b88a1b5b25bc5e70c40287554a723427c03f75515d2f764e`.
  All 13 archives were rechecked against manifest size and SHA-256 immediately
  before this launch. Their `Data/NAP/` provenance is recorded in the manifest
  [package evidence](../dynamic-lighting-intro-smoke-20261018/README.md).
- HDR/LDR configs used for the earlier timing check differ only at
  `render_hdr=true|false`; the captured live pair instead switched the Effects
  page's `HDR TONE MAPPING` checkbox in the same process. Bloom, shadows, SSAO,
  surface maps, PBR environment maps, diffuse IBL, and sun ambient were off;
  dynamic lighting remained on.
- Runtime reached `Finished loading game data`, `Interface loaded`, and
  `Entering system Hi02`. The log contains no `RenderMaterial.SetLights`
  exception, missing Resource-DLL warnings, or pause-label IDs 1268–1273.
  The pause-menu capture shows its labels rendered.

## Captures

| State | Image |
|---|---|
| HDR enabled | [Gameplay frame](hdr-on.png) |
| HDR disabled | [Gameplay frame](hdr-off.png) |
| HDR checked in Effects | [Options frame](options-hdr-on.png) |
| HDR unchecked in Effects | [Options frame](options-hdr-off.png) |
| Pause menu labels | [Text rendering](pause-menu-text.png) |
| Side-by-side | [HDR/LDR comparison](comparison.png) |

## Result and limits

The game was paused while the option was changed. About 3 minutes 30 seconds
elapsed between the clean frames, so this is a same-session, same-save visual
comparison, not a timestamp-locked pixel test. For the sky-only crop
`(x=0..1280, y=120..450)`, mean absolute RGB difference was 1.025/255
(p95 4/255); for the ship crop `(x=500..780, y=500..670)`, it was 2.401/255
(p95 7/255). Different cursor positions and the multi-minute pause/options
interval prevent treating this as a pixel-perfect renderer test. The checked to
unchecked Effects state and return to the same Hi02 scene exercise the live toggle
path; the log has no renderer exception before the process ended. The comparison
does not show a large visual benefit in this legacy scene. Strong HDR highlights,
emissive effects, bloom, PBR materials, transparent legacy effects, and
hardware-GPU review still need controlled reference scenes.

Mesa llvmpipe reported repeated simulation-overload warnings. No FPS or
performance conclusion is drawn from this run.
