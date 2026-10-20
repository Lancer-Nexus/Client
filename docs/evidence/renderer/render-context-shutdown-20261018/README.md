# RenderContext shutdown ownership (2026-10-18)

SDL2 and SDL3 now dispose their RenderContext in the cleanup `finally`, before
shutting down the native platform context. RenderContext first drains GPU
retirement fences, then attempts to dispose its registered shader set and
owned startup resources. Owned resources include the Renderer2D, SMAA lookup
textures, and fullscreen triangle buffer. Renderer2D also disposes its dynamic
vertex/index buffers, rich-text/font textures, dot texture, and unmanaged
BufferPool. Cleanup continues across individual disposal errors and reports
collected failures after attempting the rest. Repeated disposal is safe.

A headless regression checks the GPU-retirement callback, disposed SMAA and
text textures, fullscreen buffer state, shader registry closure, and repeated
Dispose. It does not emulate native OpenGL driver failures or context loss; both
remain required runtime checks.
