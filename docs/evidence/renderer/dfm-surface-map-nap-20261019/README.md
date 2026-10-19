# DFM Surface-map runtime A/B (2026-10-19)

## Result

LancerEdit's Lit ModelViewer loaded the real `li_male_elite_body.dfm` and
displayed the LOD0 body material `li_elite_male_1.tga`. The material tree
resolved the packaged supplemental normal, roughness, and metallic entries.
The Surface normal and Surface roughness/metallic switches changed the same
rendered DFM immediately without reloading the document.

The A/B kept the camera, model, OpenGL session, diffuse image-based lighting,
and environment-reflection setting fixed. The model viewport crop was
`(650,130)-(1200,875)`. Against the same image with Surface maps disabled:

| Comparison | Changed model-mask pixels | Mean absolute channel delta | RMSE |
|---|---:|---:|---:|
| Normal map on | 4,795 / 119,177 (4.02%) | 0.14592 | 1.38099 |
| Roughness/metallic maps on, after normal map | 8,090 / 119,177 (6.79%) | 0.61835 | 3.90421 |

These numbers prove that the DFM material path samples the optional maps; they
do not measure material quality. The QA fixture applies an LI ship-hull map to
a character-suit diffuse texture because no character-specific Surface set is
available. Its visible effect is diagnostic and is not a production art
recommendation. Skeletal animation, other characters, and the in-game room
scene remain unverified here.

## Captures

The three images are from the same ModelViewer session and camera:

- Surface maps off: [`dfm-nap-surface-off.png`](dfm-nap-surface-off.png)
- Normal map only: [`dfm-nap-normal-only.png`](dfm-nap-normal-only.png)
- Normal, roughness, and metallic maps: [`dfm-nap-normal-plus-pbr.png`](dfm-nap-normal-plus-pbr.png)

## NAP provenance

The QA mapping was staged as a generated test asset under
`Data/NAP/Surface/LancerEdit/SurfaceMapsWorkspace.ini`. A fresh Surface
container was built only from `Data/NAP/Surface/` and verified with
`nexus-pack verify`: 46 entries. Its SHA-256 is
`ced2da87a4605dc054bc7a5b122ec45ec0db25b81f50744b354b25333a83ef0c`.
The exact source mapping is preserved as
[`SurfaceMapsWorkspace.ini`](SurfaceMapsWorkspace.ini).
The temporary active manifest was `lancer-dfm-qa-20261019`; every package
size and SHA-256 matched the manifest before launch. The other 12 active
containers passed `nexus-pack verify` before the Surface replacement. The
manifest used for the run is preserved as [`active-manifest.json`](active-manifest.json).

The test data root contained only `EXE/` configuration files and no loose
`DATA` directory. The opened DFM came from `Data/NAP/Models/` and its bytes
match the `characters/bodies/li_male_elite_body.dfm` entry in `models.nap`
(`eb4d95185b9fd50d7472bfed9ad92ded61854c184c946b10e144ed6038b79322`). The
normal-map TXM was `fx/normalmaps_ships.txm` from the verified Surface NAP.

## Runtime

- LancerEdit Release output, LibreLancer `03bfd87-git (20261017)`.
- OpenGL 4.6, Mesa 23.2.1, Intel HD Graphics 4600 (Haswell).
- Game data loaded in 1.840 seconds; log: [`LancerEdit.log.txt`](LancerEdit.log.txt).
- The package root and build output were restored after capture; the QA
  manifest and Surface archive are not active outside this test evidence.
