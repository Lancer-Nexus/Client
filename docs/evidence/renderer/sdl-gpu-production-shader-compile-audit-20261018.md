# SDL_GPU production shader compile audit

## Command and result

Release build:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet build src/lancer/lancer.csproj -c Release \
  -p:UseSharedCompilation=false --no-restore -v:q
```

Result: 0 build errors. The build reported 168 existing compiler warnings.

GPU audit:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet run --project src/lancer/lancer.csproj -c Release \
  --no-build -- --sdl-gpu-shader-compile-audit
```

Result on 2026-10-18: exit code 0, SDL_GPU driver `vulkan`, 25 embedded bundles,
3,487 nonempty feature variants and 6,974 successfully created shader stages.
Each SDL_GPU shader object was released before the next variant. The driver
reported `MESA-INTEL: Haswell Vulkan support is incomplete` twice during startup.

## Scope

This exercises the real SDL_GPU shader creation path and confirms that the
active Vulkan driver accepts every current production SPIR-V module and its
declared sampler, storage and uniform-buffer counts. It does not build a
graphics pipeline for every variant, validate all Freelancer vertex layouts,
textures or render targets, or execute the full game/editor renderer through
SDL_GPU. Haswell Vulkan support is incomplete, so repeat this audit on the
target RTX 5060 system and another supported driver before release acceptance.
