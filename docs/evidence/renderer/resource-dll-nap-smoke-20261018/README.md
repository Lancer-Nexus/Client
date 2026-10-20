# NAP Resource-DLL UI text smoke (2026-10-18)

The two captures isolate the pause-menu labels. Before the fix, the five menu
buttons rendered without text. After the fix, all five labels render. Camera and
background differ between launches; the comparison is limited to text visibility.

## Runtime

- Client rebuilt from the current patch overlay, including patch 2359.
- Isolated Xephyr 1280x720 session using OpenGL 4.5 / Mesa llvmpipe.
- NAP-only app root; no loose `DATA`/`data` directory.
- Active manifest `lancer-nebula-ambient-smoke-20261018`; all 13 archive sizes and
  SHA-256 values matched `packages/active.json`. The manifest's archives were
  built from `Data/NAP/` inputs.
- Dynamic lighting off, sun ambient on; both runs entered `FP7_system`.
- Before patch 2359, the log warned that resource DLLs were missing and reported
  pause string IDs 1268-1273 missing. The archive contained the DLLs under `EXE/`.
- After patch 2359, no `Dll:` warnings or pause-menu missing-string warnings were
  logged, and the labels are visible. The run remained in the system without the
  previous `RenderMaterial.SetLights` exception.

## Captures

- [Before: labels missing](before-missing-resource-dlls.png)
- [After: labels visible](after-resource-dll-fallback.png)
