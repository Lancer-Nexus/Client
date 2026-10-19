# RenderScale-aware model culling (2026-10-18)

`ModelRenderer.Update` applies `RenderScale` to the model's world matrix. Before
this fix, `OutOfView`, visible-part preparation, shadow-depth culling and fog
radius calculations still used the unscaled mesh radius. A large scaled model
could therefore be culled while part of its rendered geometry intersected the
view or shadow frustum; fog-distance culling also used the smaller radius.

All model-part bounds now multiply the authored radius by the absolute render
scale before those tests. This matches the world transform for the supported
uniform scale and keeps negative scale magnitudes valid. `ModelRendererLodTests`
checks scale 18, negative scale and zero scale.

The tests verify the radius calculation and code-path integration, not a
runtime screenshot. A scene with a scaled mission waypoint and its shadow still
needs visual confirmation.
