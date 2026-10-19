# SDL_GPU production pipeline audit with Freelancer vertex layouts

Date: 2026-10-19

## Scope

The SDL_GPU pipeline audit now receives the real `VertexDeclaration` objects
from the production `FVFVertex` and `DfmTangentVertex` types. `Basic_FVF` and
`PBR` use an interleaved Freelancer FVF declaration with position, normal,
diffuse color and four UV sets. The skinned bundles use the production DFM
declaration with two UV sets, float bone weights, unsigned byte bone IDs, and
two tangent components. The normal-mapped skinned vertex shader declares
`boneIds` as `uint4`, matching the unsigned integer GPU input. All other bundles
continue to use their reflected synthetic audit layout.

This checks SDL_GPU pipeline creation and release with those selected vertex
formats. The separate SDL_GPU smoke also builds its embedded production PBR
draw pipeline from the actual `FVFVertex` declaration for `XYZ|NORMAL|TEX1`
(stride 32), executes three production PBR draws and validates the final pixel
readback. Its triangle vertices, material data and BC5 maps remain synthetic.
Neither check loads a retail VMesh/DFM asset, resolves a retail material,
exercises the game render graph, or proves complete visual correctness or
runtime performance. The Vulkan driver on this Intel Haswell test host reported
incomplete support, so these are pipeline/API checks on this host, not a general
hardware compatibility claim.

## Verification

Release build (`net10.0`, .NET SDK/runtime rooted at `/opt/dotnet-10`):

```text
dotnet build src/lancer/lancer.csproj -c Release --no-restore -p:UseSharedCompilation=false
Build succeeded. 0 errors.
```

Focused vertex-layout tests:

```text
dotnet test src/LibreLancer.Tests/LibreLancer.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter FullyQualifiedName~SDL3GpuVertexInputLayoutTests
Passed: 3, Failed: 0, Skipped: 0.
```

Production pipeline audit on the available Vulkan/Intel Haswell SDL_GPU driver:

```text
SDL_GPU_DRIVER=vulkan dotnet src/lancer/bin/Release/net10.0/lancer.dll --sdl-gpu-production-pipeline-audit
SDL_GPU production pipeline audit passed: 25 bundles, 3487 pipelines created and released.
```

The `lancer` entry point supplies `FVFVertex(XYZ|NORMAL|DIFFUSE|TEX4)` and
`DfmTangentVertex` declarations to the audit for the four matching bundles.
The other 21 bundles still use reflected packed audit inputs.

Production PBR draw and pixel readback on the same driver:

```text
SDL_GPU_DRIVER=vulkan dotnet src/lancer/bin/Release/net10.0/lancer.dll --sdl-gpu-smoke
Using production Freelancer FVF input declaration: stride=32, attributes=3.
Presented SDL_GPU BC5 normal-mapped scene through an RGBA16F target and depth-tested pass at 640x360; center pixel (226, 214, 230, 255), production PBR pixel (148, 78, 0, 255), visible-shadow pixel (54, 33, 9, 255), and shadowed PBR pixel (0, 0, 0, 255) passed.
```

Mesa printed `Haswell Vulkan support is incomplete`; despite that driver warning,
both checks completed successfully. The production draw uses test geometry and
synthetic material and texture contents; it does not submit retail mesh data.
