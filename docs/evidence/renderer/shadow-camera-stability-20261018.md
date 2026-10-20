# Directional shadow camera stability (2026-10-18)

The shadow camera previously translated its light view to the center of the
visible camera frustum on every frame, then snapped the frustum bounds in that
moving light-space coordinate system. Because the view translation followed
the same center that was being snapped, sub-texel camera motion still moved the
shadow map in world space.

The view now keeps its light-plane XY origin fixed in world space while its
depth origin follows the fitted camera slice along the light direction. The
orthographic XY center is then snapped in a stable coordinate system. This
preserves positive depth range fitting while preventing continuous shadow
projection movement for sub-texel camera translations.

`DirectionalShadowCameraTests` verifies the projected XY position of a fixed
world point across a sub-texel camera move and verifies that the fitted shadow
frustum contains the visible camera frustum's near points and its distance-
limited far points.

This is camera-math evidence. It does not verify PCF bias quality, offscreen
casters that can cast into the view, or image quality in a retail game scene.
