# Synthetic Cubemap Slot 5 GPU Probe

Date: 2026-10-14

## Setup

- SDL3 hidden OpenGL context with `SDL_VIDEODRIVER=offscreen`.
- Mesa 23.2.1 on Intel HD Graphics 4600, OpenGL 4.6 Core.
- A 4x4 `TextureCube` with six constant BGRA faces was uploaded using the
  production `TextureCube.SetData`/OpenGL backend.
- A test HLSL shader declared `TextureCube` at `t5` and `SamplerState` at
  `s5`, matching `PBR.frag.hlsl`. It sampled the six cardinal directions into
  a 768x192 render target.
- The shader was compiled by the repository `LLShaderCompiler`; the render
  target was read back through `Texture2D.GetData`.

## Result

All six center samples matched the uploaded face colors:

| Direction | Expected BGRA | GPU readback BGRA |
|---|---|---|
| +X | 0, 0, 255, 255 | 0, 0, 255, 255 |
| -X | 0, 255, 0, 255 | 0, 255, 0, 255 |
| +Y | 255, 0, 0, 255 | 255, 0, 0, 255 |
| -Y | 0, 255, 255, 255 | 0, 255, 255, 255 |
| +Z | 255, 0, 255, 255 | 255, 0, 255, 255 |
| -Z | 255, 255, 0, 255 | 255, 255, 0, 255 |

![Six synthetic cube faces sampled through t5/s5](synthetic-cubemap-slot5-20261014.png)

This confirms OpenGL cube allocation, face upload, `t5/s5` binding, directional
sampling, render-target drawing and readback on this machine. It uses a small
purpose-built shader and synthetic textures. Follow-up production-shader and
real MAT/DDS results are documented in
[production-pbr-mat-cubemaps-20261014.md](production-pbr-mat-cubemaps-20261014.md).
Neither test proves a visible reflection on `LI_ELITE`.

## Follow-up

The controlled production PBR/MAT test passed; the zero-pixel ModelViewer A/B
now points to the Retail material/mesh/draw setup or a visually weak reflection.
