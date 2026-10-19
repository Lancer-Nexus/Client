# Hi02 three-scale HDR bloom smoke (2026-10-17)

The Release Client ran from the NAP-only package root `/tmp/lancer-ggx-prefiltered-20261018/app` on OpenGL 4.6 / Mesa 23.2.1 / Intel HD Graphics 4600. The `lancer-diffuse-ibl-20261018` active manifest and all 14 NAP archive sizes and SHA-256 hashes were checked immediately before launch. There was no loose application-root `DATA` directory. The run loaded the Hi02 save with HDR and bloom enabled, SSAO and antialiasing disabled.

The Debug overlay showed live GPU timer samples for Bloom extraction (0.480 ms), base horizontal/vertical blur (0.110/0.096 ms), medium horizontal/vertical blur (0.048/0.012 ms), wide horizontal/vertical blur (0.005/0.005 ms), and tone mapping (0.877 ms). The three new resolution levels therefore executed and were measured separately; these single asynchronous samples total about 0.21 ms for the additional medium and wide blurs. Samples were two frames old at capture time.

This is a shader/pass integration smoke, not a stable quality or performance benchmark. The run showed about 30.7 FPS with the Debug overlay open and logged simulation-overload warnings on this Intel HD 4600 system. The scene/camera moved, there is no same-session single-scale baseline, and the timer samples vary by frame. No FPS gain or acceptable production cost is claimed. The screenshot records the active Hi02 frame and pass list.

| Capture | Evidence |
|---|---|
| [Three-scale bloom GPU profile](bloom-pyramid-gpu-profile.png) | HDR and bloom enabled; all per-level timer entries visible |

Still required: same-pose visual comparison against the old single-scale look, bloom-disabled timing in the same scene, other GPU profiles, and tuning of the three-level weights/radius for suns, engines and weapons.
