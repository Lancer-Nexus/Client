# Diffuse IBL live A/B on Kusari Fighter (2026-10-18)

A same-process LancerEdit ModelViewer comparison isolates the `pbr_diffuse_ibl`
toggle on a material with Surface roughness/metallic mappings. The model and
active package setup are NAP-backed; no loose `DATA` directory was present.

## Conditions

- Asset: `Models/ships/kusari/ku_fighter/ku_fighter.cmp` from `Data/NAP/Models`;
  SHA-256 `8ef395e8f4358a02b50d71d8a7565b80866a625f81bc3f58a31eb62a02529fd4`.
  The same hash is present in active `models.nap`.
- Active manifest: `lancer-diffuse-ibl-20261018`, 14 packages. Every archive
  matched manifest size/SHA-256 and passed `nexus-pack verify`; the application
  data root had `EXE` configuration files and no loose `DATA` directory.
- OpenGL 4.6, Mesa 23.2.1, Intel HD Graphics 4600; current LancerEdit overlay
  build, 1280x800 window. Built-in, PBR and editor shaders compiled at startup.
- View: `Lit`, fixed camera, ambient color `#7D7C7C`, background enabled. Surface
  roughness/metallic maps and PBR environment reflections enabled. The cubemap
  preview override was `envcube_space` (catalog strength 0.35).
- A: diffuse IBL on. B: diffuse IBL off. Only the diffuse IBL checkbox changed
  between captures.

## Result

In the ship crop `(560,250)-(990,650)`, 6,405 pixels changed; normalized RMSE
was 0.0189 (about 4.82/255). The background control crop `(1000,150)-(1200,350)`
was pixel-identical. This demonstrates that the diffuse IBL switch changes
rendered output for this PBR material and catalog cubemap in the editor.

It does not establish retail cubemap orientation, in-game quality, behavior for
other material classes, GGX mip quality, or GPU cost. The environment was an
explicit preview override, not the ship's authored retail environment.

| Capture | SHA-256 |
|---|---|
| [Diffuse IBL on](diffuse-ibl-on.png) | `04e934d33be1d9b3321c566f2a554cd31f9fa24d2723c2be2e7d757f9cba59c3` |
| [Diffuse IBL off](diffuse-ibl-off.png) | `9826a68c3fb25f52a9f4c2d44ac45a464a8f2b820ec1e911d8e820cea66371de` |
