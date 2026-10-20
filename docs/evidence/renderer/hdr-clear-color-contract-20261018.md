# HDR frame clear color contract (2026-10-18)

`SystemRenderer.ClearFrameAttachments` clears the active scene target either
with the current system background color or, during a nebula transition, the
nebula fog color. The HDR scene target is a linear `HdrBlendable` target and is
later ACES tone mapped. The legacy material shaders convert those same
INI-derived RGB colors with `LinearizeLegacyInput` before shading. Passing the
raw normalized INI color directly to the HDR clear therefore skipped the
conversion applied to the drawn fog and background-adjacent geometry.

The frame clear now decodes RGB with the same piecewise sRGB EOTF used by the
shader path only when `AntialiasTarget.HdrOutputEnabled` is true. Alpha is
preserved. LDR clears are returned unchanged, including when HDR target
allocation falls back to LDR.

`RendererColorSpaceTests` checks a mid-range channel, the sRGB breakpoint, the
white point, alpha preservation and the LDR identity path. This proves the
conversion function and its intended mode behavior; it does not independently
prove that all retail INI colors were authored in sRGB or provide an in-game
visual comparison. That asset/color-space calibration remains open.
