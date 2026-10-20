# LancerEdit Lit preview with neutral ambient (2026-10-18)

The ModelViewer Lit mode now starts with neutral gray ambient color (`0.25` per
sRGB channel) instead of black. The attached NAP-only capture shows the LI Elite
from the rear in Lit mode after loading the NAP package set from the existing
isolated runtime workspace. It runs on Mesa llvmpipe through Xvfb.

This is a single diagnostic capture, not a before/after pair. It shows that
surface detail remains visible in this ModelViewer lighting setup. It does not
prove the in-game intro lighting is fixed, establish retail color calibration,
or provide a GPU performance measurement. Continue the in-game lighting
investigation with a fixed-camera scene and a hardware-backed renderer.
