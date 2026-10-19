# SDL_GPU real Surface map sampling (2026-10-19)

This diagnostic extends the indexed Freelancer VMesh smoke to resolve the
existing `SurfaceMaps.ini` metadata, build tangents with
`VMeshTangentBuilder`, upload real BC5 normal and BC4 roughness/metallic maps,
and bind the embedded PBR shader using its reflected compact descriptor map.
It remains a diagnostic path; it does not integrate SDL_GPU into the game's
production `SystemRenderer`.

## Reproducible A/B

All asset inputs were read directly from `Data/NAP/`; no NAP container or loose
Freelancer installation was used. The on/off runs use the same model, MAT file,
selected material CRC `0xF7C6A1E6` (`elite_256`) and 1,296 indexed references.
The `off` run still reads the Surface metadata to select that same mapped
material, but does not bind the Surface textures or generate tangents.

| Mode | Vertex layout | Surface texture uploads | Production PBR pixel |
| --- | ---: | --- | --- |
| Surface off | 32 bytes | none | `(38, 42, 54, 255)` |
| Surface on | 48 bytes | BC5 normal, BC4 roughness, BC4 metallic | `(24, 27, 34, 255)` |

The Surface-on run resolved
`fx\\normalmaps_ships.txm:nm_li_elite_hull`, with roughness and metallic
factors both `1.0`. It created shader variant 519 with five fragment samplers
and used the reflected binding map `t0->0, t2->1, t3->2, t4->3, t8->4`.
Both A/B runs passed all four VMesh coverage samples and the diagnostic's depth
and PBR readback checks. The pixel delta proves the maps affect shader output;
it does not establish that the resulting material appearance is artistically
correct.

The separate no-Surface-root regression run also passed. It selected the
ordinary LOD material and retained the 32-byte production FVF declaration.
Logs for all three runs are in this directory.

## Commands and environment

Release build:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet build src/lancer/lancer.csproj -c Release \
  --no-restore -p:UseSharedCompilation=false
```

The A/B runs used:

```sh
SDL_GPU_DRIVER=vulkan DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet src/lancer/bin/Release/net10.0/lancer.dll \
  --sdl-gpu-vmesh-smoke \
  ../Data/NAP/Models/ships/liberty/li_elite/li_elite.cmp \
  ../Data/NAP/Material/ships/liberty/li_playerships.mat \
  ../Data/NAP/Surface on
```

The same command with final argument `off` produced the control result. Omitting
the Surface root exercised the no-map regression path.

Runtime used SDL_GPU's Vulkan driver on Intel Haswell through Mesa, which logged
that Haswell Vulkan support is incomplete. Repeat visual and performance
acceptance on the RTX 5060 system. This smoke draws one selected VMesh in a
640x360 diagnostic target; it does not verify a complete game scene, gameplay
performance, or cross-vendor/backend support.

## Source hashes

```text
c3323595bf0fded56c1c5072efbf3260ee74b6d2b86d40e4b8cffd8d8e52b22a  Data/NAP/Models/ships/liberty/li_elite/li_elite.cmp
0ba0b563a9562287336e23777f9e6f1e5138d48deaa08fd15a7611d06377e02c  Data/NAP/Material/ships/liberty/li_playerships.mat
dff41a54f4138b687d50ea8c11ae38b3d7e21a1ff4fafbce4ee8bd3b3f35938b  Data/NAP/Surface/fx/SurfaceMaps.ini
482cc62e05372a027df508afe1c69f2e1203e7745edd7f7c64111bfba5c601c2  Data/NAP/Surface/fx/normalmaps_ships.txm
```
