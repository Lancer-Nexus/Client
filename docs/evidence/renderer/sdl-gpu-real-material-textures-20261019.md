# SDL_GPU real VMesh material texture sampling

Date: 2026-10-19

## Scope

Extend `--sdl-gpu-vmesh-smoke` to read an external Freelancer `.mat` file, resolve
the selected VMesh material and its MAT-owned texture library, and upload/sample
the diffuse DDS through the embedded production PBR shader. The smoke also
forwards the MAT `Dc` value. The BC5 normal input remains synthetic.

## Runtime runs

Both runs used NAP source files under `Data/NAP/`, SDL_GPU with Vulkan/Mesa on
Intel Haswell, a 640x360 RGBA16F scene target, production PBR variants with and
without shadows, and indexed VMesh coverage readback.

- `li_elite.cmp` with `li_playerships.mat`: selected
  `Li_elite_lod1021202161040.3db/0CF9309F`, material CRC `0xE89BB35A`, material
  `elite_lod` (`DcDt`, diffuse `Li_elite_textures_lod_256.tga`), and
  `Dc=(1,1,1,1)`. The MAT TXM resolved the texture to a 43,816-byte DXT1 DDS,
  256x256 with 7 mip levels. Exit code 0; 1/4 interior samples differed from
  clear; PBR readback `(92,109,125,255)`.
- `transport_large.cmp` with `utility_transport.mat`: selected
  `trans_tail_fin_lod1020926170242.3db/E5523F20`, material CRC `0xF7062051`,
  material `utility_panels_01M` (`DcDt`, diffuse `utility_panels_01.TGA`), and
  `Dc=(0.631,0.631,0.631,1)`. The MAT TXM resolved a 699,192-byte DXT1 DDS,
  1024x1024 with 11 mip levels. Exit code 0; 2/4 interior samples differed
  from clear; PBR readback `(36,33,29,255)`.

The diagnostic initializes the fallback `MaterialMap` because it runs without
`FreelancerData`. Compressed DDS formats BC1, BC2, BC3, BC4 and BC5 are mapped to
SDL_GPU sampler formats; unsupported DDS types fail with an explicit message.
Mip data is uploaded through a GPU transfer buffer and retained until GPU idle.

## Verification and limits

The Release `lancer.csproj` build succeeded with 168 warnings from the applied
overlay/upstream code and no errors. The synthetic `--sdl-gpu-smoke` regression
passed. Both real asset runs uploaded all DDS mip levels and passed draw,
coverage and presentation checks. The change proves MAT/TXM resolution and
diffuse DDS sampling in the diagnostic production-PBR draw.

This does not implement SDL_GPU material handling in the game renderer. Full
VMesh ranges, CMP part transforms, real normal/metallic/roughness maps, DFM
skinning, whole-scene rendering, GL/Vulkan parity, performance and a supported
GPU matrix remain open. Mesa reports incomplete Haswell Vulkan support.
