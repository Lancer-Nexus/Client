# SSAO View-Space Hi02 smoke (2026-10-17)

This is an exploratory NAP-only runtime check of the first View-Space SSAO pass build. The captures predate the later OpenGL clip-depth correction documented in the renderer roadmap, so they are not visual validation of the current shader.
Both captures use the same Hi02 save, 1280x720 window, OpenGL/Mesa Intel HD
Graphics 4600, no antialiasing, HDR disabled, and the same camera position.
The only requested renderer difference is `--render-ssao=on` versus `off`.
The app loaded the active NAP package manifest; no loose `DATA` directory was
used.

| Mode | Screenshot | Debug overlay |
|---|---|---|
| SSAO on | [ssao-on.png](ssao-on.png) | Pass reported 0.759 ms GPU time, 2 frames old; FPS 40.50 |
| SSAO off | [ssao-off.png](ssao-off.png) | SSAO pass not scheduled; FPS 81.76 |

Both captures show the same death dialog, asteroids and background. They do not
show a clear SSAO image-quality difference: the external objects are distant,
and the dialog obscures part of the view. FPS is not an A/B result because the
samples were taken in separate processes and the star/nebula GPU timer age and
reported time differed greatly between captures. The 0.759 ms pass sample proves
that the pre-correction pass ran on this OpenGL device; it does not measure the
corrected shader or validate normal orientation, visual tuning, or performance
across scenes or GPUs.

After the clip-depth correction, a fresh Release build and NAP-only OpenGL run
reached Hi02 again. The corrected shader produced an asynchronous SSAO sample of
0.863 ms (2 frames old) on the same Intel HD Graphics 4600; [ssao-depthfix.png](ssao-depthfix.png)
records the runtime overlay. No shader/runtime error appeared in the captured
log. The death dialog still obscures the scene, so this is execution evidence,
not image-quality acceptance.

A second pair used `--invulnerable=on` to keep the ship alive in the same Hi02
save. The on/off debug captures show that the pass is scheduled only when the
setting is on; the active sample was 1.111 ms on this GPU. Clean HUD captures are
included for visual review. These are separate process runs: combat objects and
particles move between captures, so the screenshots are not a same-frame A/B and
the FPS values must not be compared. The visible result does not yet establish
acceptable AO quality.

| Mode | Clean gameplay image | Pass/profile image |
|---|---|---|
| SSAO on | [ssao-live-on.png](ssao-live-on.png) | [ssao-live-on-debug.png](ssao-live-on-debug.png) |
| SSAO off | [ssao-live-off.png](ssao-live-off.png) | [ssao-live-off-debug.png](ssao-live-off-debug.png) |

Next checks: repeat with a close-up ship/station scene, capture a stable same-pose
on/off pair without overlays obscuring the target, and profile multiple frame
windows on identical hardware. GLES, alpha-tested geometry, fog boundaries and
DFM models remain unverified.
