# M01A frame-profile samples (2026-10-15)

Two 300-frame OpenGL profiles were exported from separate NAP-only M01A
Freeport-7 intro runs. The active manifest was `lancer-pbr-qa-20261014` with 13
packages. Before these launches, every archive passed the Packaging CLI verifier;
all manifest package sizes and SHA-256 values matched, and the app directory had
no loose `DATA`. Client build: `03bfd87-git (20261013)`.

## Capture conditions

- Mesa OpenGL 4.6 on Intel HD Graphics 4600 (Haswell GT2).
- 1280x720, `vsync = false`, `per_pixel_lighting = true`,
  `dynamic_lighting = true`, and `debug = true` in the temporary client config.
- Both runs started with `--new-game` and profiled the Freeport-7 intro around
  `timer_escort_launch`.
- The Debug overlay was enabled in both runs. The mission-trigger window was
  expanded for `frame-profile.csv` and collapsed for
  `frame-profile-collapsed-trigger-window.csv`; the object list was off.
- Camera, mission time, ships, and effect activity changed between capture
  windows. These are useful diagnostic samples, not a controlled A/B test or a
  clean gameplay benchmark. Do not attribute the metric differences to the
  collapsed window alone.

## 300-frame results

Per-frame raw data is retained in both CSV files. Percentiles use the engine's
nearest-rank calculation (`ceil((n - 1) * p)`).

| Metric | Trigger window open: p50 / p95 / p99 | Trigger window collapsed: p50 / p95 / p99 |
| --- | ---: | ---: |
| Frame interval | 12.445 / 20.576 / 27.704 ms | 6.917 / 13.667 / 18.788 ms |
| Update/draw work | 3.670 / 6.689 / 7.367 ms | 3.237 / 5.163 / 11.763 ms |
| FPS samples | 80.36 / 111.63 / 114.43 | 144.61 / 162.23 / 167.80 |
| GPU stars/nebula pass | 0.302 / 0.364 / 0.424 ms | 0.327 / 0.435 / 0.484 ms |
| GPU transparent pass | 0.495 / 0.809 / 2.132 ms | 0.890 / 1.683 / 3.476 ms |

Each CSV has 300 samples for depth, opaque, stars/nebula and transparent passes;
GPU sample age is at most two frames. The AA-resolve pass has no samples. The
higher transparent-pass costs in the collapsed-window capture coincide with a
later, busier point in the moving intro, so they are not an optimization
regression measurement.

The collapsed-window run also logged 11 slow server updates (17.20–100.51 ms)
and 10 client simulation-overload notices dropping 59 stale ticks in total.
These notices span startup and the intro run; they are not scoped to the 300
profile rows. They warrant a separate repeatable simulation/performance audit.

Both captures require the Debug overlay and therefore include its UI cost. The
visible overlay screenshots document the two conditions. The capture harness
closed the app by destroying its X11 window; shutdown emitted `BadWindow`, so
clean application shutdown was not verified.

## Follow-up server update stage sample

A separate Release run was captured with the Debug overlay disabled, normal maps
off, and the same verified active NAP snapshot. It used the same Intel HD
Graphics 4600/Mesa system and reached the M01A intro. A diagnostic-only change
split slow server updates into local packet processing, one queued world request,
player work, world updates, listener work and cleanup. Only updates above the
16.67 ms threshold include this breakdown in the log.

The run recorded 10 slow updates (18.28–95.66 ms). The 95.66 ms update spent
93.76 ms in the queued world request; local packet processing took 0.08 ms.
Source tracing shows this request invokes local save-game/mission startup and
`SpaceInitialSpawn` for M01A. That is a strong lead, but the inner work is not
instrumented, so it does not prove which operation accounts for the full delay.
The next 75.33 ms update spent 75.06 ms in the first active FP7 world update.
Most later slow updates were in world updates, with several player-work spikes
(up to 17.82 ms). Listener and cleanup work were negligible in these samples.

The client separately logged 4 simulation-overload notices and dropped 30 stale
ticks (`27, 1, 1, 1`). The 27-tick drop coincided in time with the first active
world update, but the server and client loops are separate; causation is not
proven. No renderer crash occurred. Closing the X11 window again emitted
`BadWindow` and an OpenAL cleanup warning, so clean shutdown remains unverified.
This is diagnostic evidence only; it does not demonstrate an optimization or
represent the other scene types required by the system-wide performance task.
