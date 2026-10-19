# SDL_GPU production shader bundle audit (2026-10-18)

The new no-device audit command enumerates every public `ShaderBundle` field in
`LibreLancer.Shaders.AllShaders`, opens its embedded `.bin` resource, and parses
every compiled feature variant through `SDL3GpuShaderProgramReader`. The reader
validates versioned SPIR-V payload lengths and the compact SDL_GPU descriptor
slot map. Empty `NULL` feature entries are counted and skipped.

Command:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet run --project src/lancer/lancer.csproj -c Release \
  --no-build -- --sdl-gpu-shader-bundle-audit
```

Result: all 25 built-in bundles parsed. All 3,487 compiled feature variants use
shader bytecode version 2 with a validated SDL_GPU slot map; there were no
legacy variants and no empty feature entries. `Basic_FVF` and `Basic_PositionColor`
were 1,024 and 64 variants respectively; `Basic_Skinned` had 256; `PBR` and
`PBR_Skinned` each had 1,024. The remaining bundles are enumerated in the
command output and are all included in the totals.

This audit validates the serialized shader inputs and binding maps only. It does
not create GPU pipelines for every permutation, check each shader's device
resource limits, render real Freelancer meshes, or execute the game render graph.
Only the separately documented PBR smoke variants have been created and drawn
on Vulkan so far.
