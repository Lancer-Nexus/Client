# HDR shader color-space source audit (2026-10-18)

This audit traces the scene-color path from parsed legacy values through fragment
shaders and post-processing. It is a source and regression-test audit, not a
retail color calibration or whole-game visual acceptance.

## Source findings

- `RenderContext.UpdateRenderColorSpace` exposes whether the active scene target
  is HDR through the shared `RenderColorSpace` shader block.
- `includes/OutputColor.hlsl` centralizes piecewise sRGB EOTF/OETF helpers.
  `LinearizeLegacyInput` decodes only for HDR, preserving the historical LDR
  input behavior. `ToLinearSceneOutput` leaves linear scene values untouched;
  `ToDisplayOutput` encodes the PBR LDR output and leaves HDR linear.
- All 19 in-game fragment shaders under `src/LibreLancer/Shaders/` were checked.
  Legacy material, particle, sprite, sun, nebula, navmap, atmosphere, zone-volume
  and asteroid-band paths use the shared color-space helpers. PBR explicitly
  decodes albedo, vertex tint, lighting and emission, then encodes only the LDR
  display output. Depth-only variants do not write scene color.
- `includes/Lighting.hlsl` decodes ambient, diffuse, fog, material and vertex
  colors in HDR. `SystemRenderer.ClearFrameAttachments` uses the matching CPU
  sRGB decode for system and nebula clear RGB while preserving alpha. LDR clear
  values remain unchanged.
- `Tonemap.frag.hlsl` applies ACES to the linear HDR scene plus bloom and then
  encodes the display output to sRGB. Bloom and SMAA operate on the scene/resolve
  data before tone mapping in the configured pass order.
- No source-level color-space mismatch was found in these paths. The audit does
  not prove authored retail INI/MAT colors use sRGB, visual equivalence between
  legacy and PBR material modes, or correct compositing for every real effect.

## Focused verification

With the local .NET 10 runtime selected via `DOTNET_ROOT=/opt/dotnet-10`:

- `RendererColorSpaceTests`: 2 passed, 0 failed. The build compiled the
  dependent renderer projects and their shader bundle.
- `TransientLightTests`: 21 passed, 0 failed. This includes HDR sun-ambient
  tinting and preservation of the legacy LDR range.

Commands:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 PATH=/opt/dotnet-10:$PATH \
  /opt/dotnet-10/dotnet test src/LibreLancer.Tests/LibreLancer.Tests.csproj \
  --no-restore -p:UseSharedCompilation=false \
  --filter FullyQualifiedName~RendererColorSpaceTests

DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 PATH=/opt/dotnet-10:$PATH \
  /opt/dotnet-10/dotnet test src/LibreLancer.Tests/LibreLancer.Tests.csproj \
  --no-restore -p:UseSharedCompilation=false --no-build \
  --filter FullyQualifiedName~TransientLightTests
```

## Still required

Capture fixed-camera LDR/HDR pairs in representative retail scenes covering
opaque and transparent legacy materials, nebula fog, particle effects, emission
and PBR. Repeat on a hardware GPU, compare against the original client where
possible, and record color-space assumptions for each asset source. The current
unit tests validate conversion arithmetic and CPU lighting behavior only.
