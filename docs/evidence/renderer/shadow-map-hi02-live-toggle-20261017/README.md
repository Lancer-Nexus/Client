# Hi02 shadow-map live-toggle evidence (2026-10-17)

The current Release build was launched from the NAP-only application copy on
OpenGL 4.6 / Mesa 23.2.1 / Intel HD Graphics 4600. It loaded the same Hi02 save
used by the SSAO smoke, with `--invulnerable=on`, `--render-shadows=on`, SSAO,
HDR and antialiasing disabled. The active package manifest and all 14 archive
hashes/sizes had been verified immediately before these runtime checks; there
was no loose root `DATA` directory.

The original enabled debug capture reports `Shadow map: 0.216 ms (2 frames old)`.
In a later Release run with the diagnostic patch, the scrolled Debug window
reports `Shadow map: active 'Hi02_walkerlight' dir rgb=(0.824,0.463,0.102)
Y=0.513` and `Shadow map: 0.111 ms (2 frames old)`. Hi02 has two persistent
directional sources; `Hi02_walkerlight` wins the current Rec. 709 brightness
selection. The capture confirms which source the code uses, but does not prove
that this is the visually correct source against the retail game.

Patch 2244 adds an optional `render_shadow_source` setting and the matching
`--render-shadow-source=auto|<light-nickname>` startup override. A Release
NAP-only run with `--render-shadow-source=Hi02_system_light` loaded Hi02 and the
Debug window reported `Shadow map: active 'Hi02_system_light' dir
rgb=(0.910,0.314,0.000) Y=0.418`. Thus `auto` and explicit selection choose the
expected different active directional sources. This confirms selection
behavior only; separate-process screenshots are not used as a visual quality
comparison.

The setting was then tested from `[Settings]` in the configuration file, with
no `--render-shadow-source` argument. The Release NAP-only run again reported
`Shadow map: active 'Hi02_system_light' dir rgb=(0.910,0.314,0.000)
Y=0.418`. The Debug overlay showed the parsed setting, and the startup log had
no unknown-entry warning for `render_shadow_source`.

In the earlier live-toggle process, F1 paused gameplay and the Performance
options showed `DIRECTIONAL SHADOW MAPS (BASIC / PBR)` checked. The checkbox was
turned off and the same menu remained open. A pixel comparison of the 200×130
upper-left world region outside the menu and HUD found 9,723 of 26,000 pixels
changed by more than 2/255, with mean absolute RGB difference 9.91/255. This
shows a visible same-session response. The capture timestamps were not
recorded, so exact frame identity is not proven and the image does not isolate
shadowing from other renderer behavior coupled to the setting.

A tighter same-process capture was made with the Performance menu open. The enabled image was captured at `2026-10-17T09:53:57.088227824+02:00`; after one checkbox click, the disabled image was captured at `2026-10-17T09:53:57.547722069+02:00` (0.46 seconds later). The checked state and the change in the exposed Hi02 scene are visible in the pair below. The capture is useful for confirming immediate setting response, but the menu obscures much of the scene and the remaining exposed scene can still animate; it is not a frame-identical shadow quality or performance benchmark. The earlier timestamp-free pair remains historical evidence only.

| Capture | Evidence |
|---|---|
| [Same-session shadows enabled](shadow-on-same-session-20261017.png) | Performance-menu checkbox checked |
| [Same-session shadows disabled](shadow-off-same-session-20261017.png) | Checkbox unchecked after the click, 0.46 seconds later |

A separate Release run with `--render-shadows=off` reached the same Hi02 save.
Its debug overlay reports `Shadow map: waiting for GPU`, confirming no shadow-map
sample was scheduled in that run. The two FPS values are from separate runs and
are not comparable; the current scene contains moving effects and the GPU timer
ages differ.

| Capture | Evidence |
|---|---|
| [Enabled pass](shadow-on-debug.png) | Shadow-map GPU pass scheduled; one 0.216 ms sample |
| [Selected light](shadow-selected-light-debug.png) | Later Release run names `Hi02_walkerlight` and shows a 0.111 ms sample |
| [Explicit source](shadow-selected-system-light-debug.png) | `--render-shadow-source=Hi02_system_light` selects the requested source |
| [INI source](shadow-ini-source-debug.png) | `render_shadow_source` in `[Settings]` selects the same source without a CLI override |
| [Enabled option](shadow-on-options.png) | Performance checkbox checked in the paused session |
| [Disabled option](shadow-off-options.png) | Checkbox unchecked after the live toggle |
| [Enabled gameplay](shadow-on-live.png) | Clean-HUD view from the enabled run |
| [Disabled gameplay](shadow-off-live.png) | Clean-HUD view after toggling off in that process |
| [Disabled pass](shadow-off-debug.png) | Separate off run reports that the pass is waiting for GPU |

This verifies pass scheduling and live setting application on this OpenGL
configuration, with a measurable same-session scene response. It does not
calibrate shadow bias, filtering, distance, caster coverage, other material
families, frame-identical image quality or FPS cost. A controlled same-pose ship/base comparison, unobstructed capture, and other GPUs remain outstanding.
