# SDL_GPU production graphics pipeline audit

## Build and runtime

Release build:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet build src/lancer/lancer.csproj -c Release \
  -p:UseSharedCompilation=false --no-restore -v:q
```

Result: 0 errors and 168 existing compiler warnings.

Pipeline audit:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet run --project src/lancer/lancer.csproj -c Release \
  --no-build -- --sdl-gpu-production-pipeline-audit
```

Result on 2026-10-18: exit code 0 on the SDL_GPU Vulkan driver (Mesa Intel
Haswell). The probe created and released 3,487 graphics pipelines across all 25
nonempty production shader bundles. This includes 1,024 Basic_FVF, 256
Basic_Skinned, 1,024 PBR, and 1,024 PBR_Skinned permutations. Mesa reported that
Haswell Vulkan support is incomplete.

## What this checks

For each compiled production vertex/fragment pair, the probe reflects vertex
attribute locations and 32-bit scalar/vector types from SPIR-V, creates the
SDL_GPU shader objects, creates a pipeline with those reflected attributes and
an RGBA8 color target, then releases all GPU objects. Any failure reports the
bundle and feature mask.

This checks pipeline creation against the current shader interfaces, but it
does not draw with retail Freelancer mesh buffers, map semantic inputs to the
actual FVF byte offsets, bind production material textures, or exercise the
production depth/render-target formats. The separate FVF3 PBR draw smoke covers
one production PBR variant with synthetic geometry. The complete SDL_GPU game
and editor render paths remain unimplemented. Repeat on the target RTX 5060 and
a supported driver before release acceptance.
