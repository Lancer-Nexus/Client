# SDL_GPU shader bundle reader smoke (2026-10-18)

The shared `SDL3GpuShaderProgramReader` was exercised by the existing release
smoke after extracting shader payload parsing from `SDL3GpuSmoke`. It parses the
versioned SPIR-V stages and verifies that each compact descriptor binding is
represented by the serialized logical-to-SDL slot map. Multiple SPIR-V variables
may share a logical binding (for example, paired texture and sampler resources);
the validator compares distinct set/binding slots while retaining distinct
logical map entries.

Command:

```sh
SDL_GPU_DRIVER=vulkan DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet run --project src/lancer/lancer.csproj -c Release \
  --no-build -- --sdl-gpu-smoke
```

Result: SDL selected Vulkan and reported the expected incomplete Haswell Vulkan
support warning. Both the standard production PBR variant (feature mask 512)
and the shadowed variant (mask 768) loaded. The FVF3/index-buffer, BC5 texture,
D32 depth target, sampleable shadow map and RGBA16F scene target checks passed.
Readback pixels matched the established tolerances: BC5 scene `(226, 214, 230,
255)`, production PBR `(148, 78, 0, 255)`, visible shadow `(54, 33, 9, 255)`,
and shadowed PBR `(0, 0, 0, 255)`.

This confirms the shader bundle reader on the Vulkan smoke path. The SDL_GPU
probe still does not run the game render graph or real Freelancer scene assets.
