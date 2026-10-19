# DFM PBR NAP package preflight (2026-10-15)

The initial DFM map-toggle comparison was pixel-identical because the selected
material had no Surface PBR binding. For a follow-up, the existing QA mapping
was copied into `Data/NAP/Surface/fx/SurfaceMapsDfmQa.ini` before packaging.
It assigns the existing LI Elite hull roughness/metallic nodes to
`li_elite_male_1.tga`, with normal strength `0` and factors `0.55`/`0.45`.
This is a shader-path diagnostic mapping, not character-authored material data.

The temporary Surface package was packed directly from `Data/NAP/Surface`.
Its 42 archive entries passed `nexus-pack verify`; each path, length and SHA-256
matched the corresponding source file under `Data/NAP/Surface`. The isolated
test installation used active manifest
`lancer-dfm-pbr-data-nap-20261015`; all 13 active packages passed size, SHA-256
and NAP content verification. No loose `DATA` directory was present.

LancerEdit was started from that isolated installation with the DFM asset from
`Data/NAP`. The process logged SDL3 and engine initialization but produced no
further log entries or usable window. `xdpyinfo -display :0` also timed out,
showing the current Xwayland display was unresponsive. The isolated process was
stopped. No follow-up screenshot or PBR pixel comparison was obtained, so the
DFM PBR visual path remains unverified. Retry the same package after the local
display responds; then capture same-process PBR maps OFF/ON and confirm a
nonzero viewport delta before assessing material quality.

Temporary package: `/tmp/lancer-renderer-dfm-pbr-qa-20261015/output/surface.nap`

- Size: `1163429462` bytes
- SHA-256: `b696f5ec93f76c86d322f9fba563d99ce3ff65632827a2da98970674e0bc71af`


## Native Wayland startup retest

A later retest used the same isolated installation with `SDL_VIDEODRIVER=wayland`
after Xwayland remained unresponsive. The active manifest was rechecked: all 13
package files matched their recorded sizes and SHA-256 values, and the app root
still had no loose `DATA` directory. The apphost ran with the configured .NET 10
runtime and loaded the complete game data set in 1.249 seconds. The LancerEdit
process remained alive after data loading and was then stopped cleanly. The log
contains no crash or exception line for this run.

This confirms NAP-backed editor startup on Wayland, but not a visible DFM material
comparison: the GNOME Shell screenshot call returned `Screenshot is not allowed`,
and no viewport image or pixel delta was obtained. The DFM PBR visual path therefore
remains unverified.
