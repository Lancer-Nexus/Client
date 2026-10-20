# Intro lighting smoke (2026-10-18)

This check followed the `RenderMaterial.SetLights` null-reference report and
compared the Freelancer M01A intro with dynamic lighting disabled and enabled.
Both runs used the same freshly built Release client and the same active NAP
snapshot. They reached `FP7_system` without a renderer exception.

## Build and runtime

- `python3 scripts/verify-lancer-nexus-overlay.py`: 988 files matched a clean
  reconstruction of the patch series.
- Release build: `dotnet build src/lancer/lancer.csproj --no-restore
  -p:UseSharedCompilation=false -c Release`; 0 errors, 129 existing warnings.
- OpenGL 4.5 / Mesa llvmpipe, Xephyr 1280x720.
- Manifest: [`nap-active.json`](nap-active.json), ID
  `lancer-nebula-ambient-smoke-20261018`, SHA-256
  `ba8f9b763c251e99b88a1b5b25bc5e70c40287554a723427c03f75515d2f764e`.
  All 13 archive sizes and hashes matched the manifest and each archive passed
  `nexus-pack verify`; the package root was a real directory and the app root
  had no loose `DATA` or `data` directory. Package inputs are under `Data/NAP/`.
- Both runs used `--render-sun-ambient=on --render-hdr=on
  --render-bloom=off --new-game`; only `--render-dynamic-lighting` differed.
- The off run reached `Finished loading game data`, `Interface loaded`, and
  `Entering system FP7_system`. It did not log the reported
  `RenderMaterial.SetLights` exception.
- The on run reached the same milestones and rendered the station visibly lit.
  The green nebula sky rendered in both.

## Captures

| Setting | Capture |
|---|---|
| `--render-dynamic-lighting=off` | [Dark station](dynamic-lights-off.png) |
| `--render-dynamic-lighting=on` | [Lit station](dynamic-lights-on.png) |

These captures are from separate points in the intro camera path, not a
pixel-aligned A/B pair. They support diagnosing the dark intro as correlated
with disabled dynamic lighting; they do not establish final lighting
calibration or performance. A same-process option change also reproduced the
dark station after returning to the game with dynamic lighting off.
