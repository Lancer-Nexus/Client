# SDL_GPU skinned DFM Surface-map smoke (2026-10-19)

## Scope

This is a diagnostic draw/readback for one skinned DFM and the production Freelancer PBR shader variants. It loads the DFM and its supplemental Surface assets through the active `NexusPackageFileProvider`; no loose Freelancer data is mounted.

## NAP provenance

The QA NAP was packed from seven files staged under `Data/NAP/`: the test DFM, the Surface map INIs, the LancerEdit workspace map INI, and `fx/normalmaps_ships.txm`. `provenance.json` records each source path, size, and SHA256. The archive was inspected and verified before launch.

- Package id: `dfm-qa-20261019`
- Package UUID: `d8961bcd-51bc-24c5-8660-a64988bdbca5`
- Version: 1
- Entries: 7
- Archive size: 37,128,596 bytes
- Archive SHA256: `e8533a08b1f45e77e7d3400b6ca815badb40105b2fa88db90dfd27735fb7c32b`
- Active manifest: `Data/NAP/.runtime-test-stage/dfm-gpu-nap-20261019/application/packages/active.json`

## Runtime commands

From `Client/` after building Release with .NET 10:

```sh
SDL_VIDEODRIVER=wayland DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet src/lancer/bin/Release/net10.0/lancer.dll \
  --sdl-gpu-dfm-nap-smoke ../Data/NAP/.runtime-test-stage/dfm-gpu-nap-20261019/application \
  Models/characters/bodies/li_male_elite_body.dfm on

SDL_VIDEODRIVER=wayland DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet src/lancer/bin/Release/net10.0/lancer.dll \
  --sdl-gpu-dfm-nap-smoke ../Data/NAP/.runtime-test-stage/dfm-gpu-nap-20261019/application \
  Models/characters/bodies/li_male_elite_body.dfm off
```

Both commands exited 0 on SDL_GPU/Vulkan, Mesa Intel Haswell. The driver printed that Haswell Vulkan support is incomplete. The ON run uploaded the MAT diffuse texture and NAP BC5 normal plus BC4 roughness and metallic mip chains; the OFF run omitted supplemental Surface maps. Both executed production PBR shader variants and submitted an indexed, depth-tested skinned DFM draw.

- Material selected: `li_male_elite_1`, 618 face-group corners
- Skinning: 28 bones, 1,792-byte bone matrix buffer
- Vertex declaration: Freelancer FVF, 76-byte stride, 8 attributes
- Readback: 3 of 4 interior coverage probes differed from clear in both runs
- First reported interior sample: maps ON `(18,31,63,255)`, maps OFF `(13,24,51,255)`
- Other reported material probes: black in both runs `(0,0,0,255)`

## Limits and follow-up

The ON/OFF sample change confirms that the supplemental Surface path reaches the production shader and changes a readback pixel. It does not establish visually correct material output or acceptable frame performance. The test deliberately applies a ship hull Surface map to a character DFM, and the remaining sample probes are black. Repeat with a representative ship/base mesh, inspect a saved screenshot on a supported GPU, and validate the in-game option path before treating Surface-map rendering as visually accepted.
