# PBR legacy vertex and opacity inputs (2026-10-18)

The PBR fragment input already receives `color` at `TEXCOORD4`, but the
fragment shader did not use it when building base color. The legacy lighting
helpers multiply decoded vertex RGB into decoded material diffuse color. PBR
now applies the same linear-space vertex tint before deriving diffuse and
metallic specular colors.

`BasicMaterial` uploads `Oc * OpacityMultiplier` into the PBR parameter block.
The PBR shader declared that field but omitted it from the returned alpha.
The output alpha now multiplies base texture/material/vertex alpha by `Oc`, so
the existing material opacity and per-draw opacity reach the blend pipeline.

The PBR and skinned PBR shader bundle is built and exercised by
`DfmSkinnedPbrShaderTests`. This verifies shader compilation and variant
selection, not pixel-level alpha blending or the appearance of retail vertex
tints. Those in-game comparisons remain open.
