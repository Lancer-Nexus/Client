# Cityscape sky sphere depth fix

The before image was captured from the NAP-only `Li01_01_Base / Cityscape`
mission cutscene before the renderer fix. Its visible upper sky was black.
The after image is from the same saved mission state after the fix; the
blue-violet skydome is visible above the city models. The cutscene camera had
advanced between captures, so framing differs.

The scene ambience THN defines `ambi_skydome_sky_vrtx_blue` as a starsphere
(`usr_flg=2`) at the far depth. The default `render_depth_prepass` setting is
false. Without that prepass, frame setup left the depth function at `Less`,
which rejected the starsphere at depth 1. The stars-and-nebula pass now uses
`LessEqual` and restores its incoming depth function and range afterward.

Runtime provenance: active manifest `lancer-nebula-ambient-smoke-20261018`;
13 NAP archives packed from `Data/NAP` inputs. The active manifest sizes and
SHA-256 hashes matched, and `nexus-pack verify` passed for every archive before
the post-fix launch. The app had no loose `DATA` or `data` directory. Runtime:
Linux, Mesa OpenGL 4.6, Intel HD Graphics 4600, Release build
`03bfd87-git (20261017)`.
