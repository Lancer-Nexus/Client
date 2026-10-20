# DFM PBR viewer setup (2026-10-15)

This NAP-only LancerEdit run opened
`Data/NAP/Models/characters/bodies/li_male_elite_body.dfm` from the source
tree and rendered the textured LI male elite body in ModelViewer. The Render
tab loaded the Full Surface profile (34 libraries, 2,807 normal-map entries)
and displayed separate controls for Surface roughness/metallic maps, PBR
environment reflections, Surface normal maps, and generated tangents.

These initial screenshots document model loading and the available controls,
not a PBR effect. A later same-process OFF/ON capture of the Surface
roughness/metallic-map toggle is recorded in the
[DFM PBR map-toggle diagnostic](pbr-map-toggle-ab-20261015.md). The viewport
was pixel-identical because LOD 0 material `li_male_elite_1` had no roughness or
metallic map or scalar factor assigned. The toggle therefore did not select a
visible PBR material path for this asset. A bound skinned DFM PBR draw, visual
quality, and GPU/CPU cost remain open.

A temporary, isolated NAP-workspace entry now assigns the existing LI Elite
hull normal/roughness/metallic nodes to this character diffuse texture with
normal strength 0 and PBR factors 0.55/0.45. It is QA-only and does not alter
`Data/NAP`; a follow-up visual capture is still required.

| Image | Capture |
|---|---|
| [ModelViewer](modelviewer.png) | Textured DFM body rendered in the viewer |
| [Render controls](render-tab.png) | Full profile and independent rendering toggles |
| [PBR maps off](pbr-maps-off-20261015.png) | Same-session diagnostic capture, map toggle off |
| [PBR maps on](pbr-maps-on-20261015.png) | Same-session diagnostic capture, map toggle on |

Both A/B files are 1920×995 captures from the same ModelViewer session. The
desktop status bar was cropped out. The earlier `modelviewer.png` and
`render-tab.png` files are 800×572 crops. A headless
`DfmSkinnedPbrShaderTests` test confirms bundle selection for `DfmVertex` and
`DfmTangentVertex`, including normal mapping only on tangent vertices and the
regular PBR fallback without bone skinning. This tests shader selection, not a
GPU draw. SHA-256:

| File | SHA-256 |
|---|---|
| `modelviewer.png` | `605d6dc5918e68b6e0c25394985b796b19ed47fc23f8d90ef8354196139cc762` |
| `render-tab.png` | `324f21bdfb3d85153429c1d1b7a1c16a6c8b7f42a17ba722cb7e69b7bc23f906` |
| `pbr-maps-off-20261015.png` | `ebac84902f191c9555472053a3c1d9edd4671155f613d9b469b48161bb5ab34c` |
| `pbr-maps-on-20261015.png` | `7b13df97ee5aae94df06fcc155e623ad91f2d02eb521db4931761a12be4df114` |
