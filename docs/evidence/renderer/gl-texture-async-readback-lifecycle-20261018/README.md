# OpenGL async texture readback lifecycle (2026-10-18)

`GLTexture2D.GetDataAsync()` queues readback through a pixel-pack buffer and a
GPU fence. The previous callback copied from the mapped pointer without checking
whether mapping succeeded, did not release the PBO, and could leave its task
incomplete after a callback failure. Setup exceptions could also strand the
framebuffer or pixel-pack binding.

The updated path now:

- checks readback-size arithmetic for overflow;
- restores the render target and pixel-pack binding and releases the temporary
  framebuffer on setup exits;
- deletes the PBO and an unregistered fence if setup or fence registration
  fails;
- completes the task asynchronously only after successful map, copy, unmap,
  unbind and PBO release;
- faults the task on map/unmap/cleanup failure while still attempting PBO
  cleanup.

`LibreLancer.Base` builds successfully on .NET 10 with 0 errors. This is source
and compile evidence only. Fault injection for GL entry points and live
readback checks on Sync-capable and Sync-less drivers remain open.
