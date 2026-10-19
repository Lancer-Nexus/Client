# SDL_GPU real VMesh input smoke (2026-10-19)

## What this checks

The diagnostic command `--sdl-gpu-vmesh-smoke <cmp-path>` parses a Freelancer CMP,
selects its largest indexed draw with the exact `XYZ | NORMAL | TEX1` FVF layout,
and passes one indexed triangle through the production vertex declaration and
embedded production PBR variants. It retains the selected triangle's actual
positions, normals and UVs, projects the triangle into a stable diagnostic view,
and checks GPU readback at interior pixels. The production `VMeshRef.CreateLevel`
index-start and base-vertex rules are used.

The smoke deliberately does not claim a full model render: it uses one triangle,
a synthetic BC5 normal texture and synthetic material parameters. It does not
bind the CMP material library or retail diffuse/normal textures, apply the part
transform, load DFM skinning, or exercise the full game render graph. Shadow
pixel expectations remain checked by the synthetic smoke; they are not used as
an acceptance condition for asset-derived normals/materials.

## Input provenance

Both model files were read directly from `Data/NAP/Models/`:

| Asset | SHA-256 |
|---|---|
| `ships/liberty/li_elite/li_elite.cmp` | `c3323595bf0fded56c1c5072efbf3260ee74b6d2b86d40e4b8cffd8d8e52b22a` |
| `ships/utility/transport_large/transport_large.cmp` | `3854b4bee0156e20feaf3f69e2987c88ae11ffcf2ba1375e2d6d115e8c529c37` |

These are direct NAP source-file probes, not a test of the active NAP container
mount or its manifest.

## Build and runtime

Release build:

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet build src/lancer/lancer.csproj -c Release \
  --no-restore -p:UseSharedCompilation=false
```

The build passed with zero errors. The SDL_GPU runs used the normal driver
selection and chose Vulkan/Mesa on Intel Haswell. Mesa reports that Haswell
Vulkan support is incomplete; this is a path smoke, not GPU compatibility or
performance certification.

```sh
DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet src/lancer/bin/Release/net10.0/lancer.dll \
  --sdl-gpu-vmesh-smoke ../Data/NAP/Models/ships/liberty/li_elite/li_elite.cmp

DOTNET_ROOT=/opt/dotnet-10 DOTNET_ROOT_X64=/opt/dotnet-10 \
  /opt/dotnet-10/dotnet src/lancer/bin/Release/net10.0/lancer.dll \
  --sdl-gpu-vmesh-smoke ../Data/NAP/Models/ships/utility/transport_large/transport_large.cmp
```

Both returned exit code 0 at 640x360. The selected LI Elite draw was
`Li_elite_lod1021202161040.3db/0CF9309F`, material CRC `0xE89BB35A`, with
1,422 references in the selected draw. The selected Transporter draw was
`trans_tail_fin_lod1020926170242.3db/E5523F20`, material CRC `0xF7062051`, with
225 references. Each loaded PBR variants with and without shadow resources,
created the RGBA16F scene target, executed the three production PBR draws, and
reported successful coverage readback (1/4 LI Elite samples, 2/4 Transporter
samples differed from the scene clear).

The existing synthetic `--sdl-gpu-smoke` also returned exit code 0 after this
change, including its stricter normal-map, indexed-offset, PBR and shadow pixel
checks.

## Remaining SDL_GPU renderer work

This step proves asset parsing, production FVF layout selection, actual VMesh
position/normal/UV upload, indexed geometry coverage and production PBR shader
execution for two NAP CMP inputs. Material/texture binding, full VMesh draw ranges,
CMP part transforms, DFM skinning, real-scene lighting/shadows, render-graph
integration, presentation parity, performance and a supported desktop GPU matrix
remain open.
