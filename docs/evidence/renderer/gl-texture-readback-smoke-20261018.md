# OpenGL Texture2D readback runtime smoke (2026-10-18)

## Environment

- Release client built with the installed .NET 10 runtime under `/opt/dotnet-10`.
- Mesa 23.2.1, OpenGL 4.6 Core Profile, Intel HD Graphics 4600 (Haswell GT2).
- No Freelancer data or NAP package is loaded. The probe creates its 4×4 texture
  in memory, so package provenance is not part of this graphics-API check.

## Command and result

```sh
/opt/dotnet-10/dotnet src/lancer/bin/Release/net10.0/lancer.dll \
  --gl-texture-readback-smoke
```

Result:

```text
OpenGL Texture2D.GetData passed mip-level, region, offset/count, bounds, and pixel checks.
```

The probe uploads four known BGRA8 pixels to mip level 1 of a 4×4 texture. It
reads the full 2×2 mip into a destination with sentinel elements before and
after the requested range, then reads a 1×2 subrectangle at a nonzero output
offset. Pixel values and untouched sentinels are checked. Invalid mip level,
out-of-bounds rectangle and too-short destination are each required to throw an
argument exception.

This verifies the desktop `glGetTexImage` branch and mip/region/offset contract
on the listed Mesa driver. GLES uses a separate framebuffer/`ReadPixels` branch
and is not covered by this run. Compressed texture decompression and editor PNG
export are also outside this probe.
