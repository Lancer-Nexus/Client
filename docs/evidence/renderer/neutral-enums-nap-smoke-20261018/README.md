# Renderer enum/depth-state NAP smoke (2026-10-18)

The Release Client was rebuilt after patches 2340 and 2341 and launched in an
isolated Xephyr display. The runtime log reached `Finished loading game data`
and `Interface loaded`, then displayed the Freelancer main-menu scene. No GL
error or crash was recorded. The captured frame is `main-menu.png`.

The active NAP snapshot was `lancer-pbr-qa-20261014`; its manifest SHA-256 was
`57af6496c39b4c35ebb32add550fd58ef00f5eb40f7658b7207a06fc99293652`. All 13
archive sizes and hashes matched the manifest, all archives passed
`nexus-pack verify`, and the application directory had no loose `DATA` or
`data` directory. The package-source report for this exact manifest records
inputs staged under `Data/NAP/` in
[`M01A run 20`](../ship-light-occlusion-m01a-run20-20261018/README.md).

The renderer was OpenGL 4.5 Core on Mesa 23.2.1 / llvmpipe in the Xephyr
display. The configured run had HDR, bloom, shadows, SSAO, sun ambient, lens
glow and soft particles enabled. This confirms that the rebuilt client mounts
the verified NAP snapshot and renders the startup scene after the backend enum
changes. It is not a before/after image comparison and does not independently
prove each feature or quantify the depth comparison transition; fixed-scene
GL frame capture and hardware-GPU validation remain open.
