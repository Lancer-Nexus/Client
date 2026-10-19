# HDR sun ambient color handling (2026-10-18)

The INI parser converts `Color4` channel values by dividing each component by
255 (`src/LibreLancer.Data.Generator/SectionParser.cs`, `SupportedType.Color4`).
The read-only retail file
`/home/masterbee/Dokumente/FL/DATA/UNIVERSE/SYSTEMS/EW02/ew02.ini` contains:

- system ambient `10, 15, 12` at lines 85-86;
- directional system light `100, 150, 100` at lines 88-93;
- zone fog color `70, 100, 70` at lines 116-120.

This establishes the stored numeric range and shows that those channels enter
the game as normalized values. It does not by itself establish whether retail
intended those values as linear RGB or sRGB.

The HDR shader path currently applies the sRGB EOTF to `AmbientColor`, light
diffuse/ambient values, and fog color through `LinearizeLegacyInput` in
`src/LibreLancer/Shaders/includes/Lighting.hlsl`. `RenderHelpers.ApplySunAmbient`
combines source system ambient and system-light colors on the CPU before those
shader conversions. Patch 2310 makes this HDR-only blend decode each input to
linear RGB, perform the tint and Rec. 709 luminance preservation there, then
encode the result back to sRGB for the existing shader contract. LDR output
keeps its previous arithmetic and range handling.

Regression coverage verifies red, green, and white HDR tint results, preserved
HDR headroom, unchanged LDR range behavior, and exact no-op results when tint
strength is zero or no eligible source light exists. The focused
`TransientLightTests` run passed 21 tests after this change.

Still open: retail color-space intent has not been proven from the original
renderer, and the HDR color result has not yet been visually calibrated in a
NAP-only gameplay scene.

The sun ambient tint is also applied after a nebula supplies its own ambient
color. This prevents nebula ambient from bypassing the system sun tint; the
nebula's authored color remains the blend base. A focused regression confirms
the tint math for a green sun over a nebula ambient sample. A full NAP gameplay
capture inside a colored nebula remains outstanding.
The updated focused `TransientLightTests` suite passes all 22 tests, including
its nebula-ambient tint regression.
