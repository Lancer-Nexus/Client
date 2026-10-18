# SDL_GPU complete Freelancer VMesh material draw (2026-10-19)

## What changed

`--sdl-gpu-vmesh-smoke <cmp-path> <mat-path>` now uploads and draws the whole
largest valid FVF3 `TMeshHeader` surface selected from a CMP. It uses that
surface's complete index range, the production `VMeshRef` base-vertex rule,
positions, normals, UVs, MAT diffuse color, and the actual compressed diffuse
DDS resolved from the MAT's TXM library. The PBR draw samples the asset UVs and
uses the selected indexed surface. Buffer and index ranges are validated before
GPU upload. A separate synthetic buffer keeps the presentation-copy check
independent of the retail mesh data.

For a stable smoke view, the input vertices are projected into a plane derived
from the first triangle of the selected surface. Depth along the projection
normal is retained. This is diagnostic geometry: it does not apply CMP part
transforms, draw all surfaces/materials in the model, or load DFM skinning.
Normal mapping still uses a synthetic BC5 texture; real Surface, metallic and
roughness maps are not bound by this probe. The SDL_GPU path remains diagnostic
and is not integrated into the game's production renderer.

## NAP source files

The direct inputs came from `Data/NAP/`; no NAP archive or active manifest was
used by this diagnostic.

| Asset | SHA-256 |
|---|---|
| `Models/ships/liberty/li_elite/li_elite.cmp` | `c3323595bf0fded56c1c5072efbf3260ee74b6d2b86d40e4b8cffd8d8e52b22a` |
| `Material/ships/liberty/li_playerships.mat` | `0ba0b563a9562287336e23777f9e6f1e5138d48deaa08fd15a7611d06377e02c` |
| `Models/ships/utility/transport_large/transport_large.cmp` | `3854b4bee0156e20feaf3f69e2987c88ae11ffcf2ba1375e2d6d115e8c529c37` |
| `Material/ships/utility/utility_transport.mat` | `15a0dae0e59b7b196fed1bdfdd2242e5bebf3111c0648d259f189b08f05bafa0` |

## Verification

Release build with .NET 10 (`/opt/dotnet-10`) succeeded with 0 errors and 31
warnings. The synthetic smoke and both NAP-backed asset probes exited 0 using
SDL_GPU's Vulkan/Mesa driver on Intel Haswell.

- LI Elite: material `elite_lod`, diffuse DDS DXT1 256x256 with 7 mips; the
  selected surface submitted 1,422 indices from base vertex 0 and changed 4/4
  coverage samples.
- Transporter: material `utility_panels_01M`, diffuse DDS DXT1 1024x1024 with
  11 mips; the selected surface submitted 225 indices from base vertex 244 and
  changed 3/4 coverage samples.
- The synthetic smoke retained its exact pixel, normal-map, and shadow checks.

The raw runtime logs are alongside this report. Mesa warns that Haswell Vulkan
support is incomplete; this validates the current diagnostic path on this host,
not general driver compatibility, the full scene renderer, or performance.
