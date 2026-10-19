# Release runtime: render-target resize and restore (2026-10-18)

## Run

The current Release client ran with the updated `SystemRenderer` code on SDL3, OpenGL 4.6 / Mesa 23.2.1, Intel HD Graphics 4600. It loaded the NAP-backed Freelancer data and interface, entered `FP7_system`, and executed the M01A intro scene with HDR, Bloom, SSAO, and SMAA High enabled.

Window sizes were changed from 1280x720 to 960x540, back to 1280x720, then to 1920x1000 (the window manager reported 1920x995), back to 1280x720, and finally minimized/mapped at the same size. The process stayed alive. The captured log contains no `GL_OUT_OF_MEMORY`, renderer exception, or error during this sequence.

The change under evaluation releases the previous AA/HDR/SSAO target set before constructing the replacement, avoiding simultaneous residency of old and new full-resolution sets. The test is consistent with that reducing resize memory pressure, but it does not prove a GPU-memory budget or resolve the separate earlier `xdotool windowclose` behavior, which emitted `OCCLUDED` and `FOCUS_LOST` rather than a regular close event. No hardware other than the Intel HD 4600 / Mesa path was tested.

## NAP provenance

The active manifest snapshot is [`nap-active.json`](nap-active.json). The 13 archives were each checked against its manifest size and SHA-256, and each passed `nexus-pack verify`. All archive inputs and the Freelancer/Dacom INIs are under `Data/NAP/.runtime-test-stage/package-inputs-20261016/`; see [`package-inputs.txt`](package-inputs.txt). No loose `DATA`/`data` folder was present in the application root.

## Artifacts

- [`runtime.log.txt`](runtime.log.txt)
- [`window-sizes.txt`](window-sizes.txt)
