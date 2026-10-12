# Client patch groups

The active series contains eleven Client bundles and two native bundles. Each
file belongs to one functional bundle; the combined output preserves the
integration, upstream compatibility fixes, tests and project documentation.

`base-client-commit`, `base-lancerdecode-commit` and `base-blurgtext-commit`
pin the source trees used for reconstruction. Update a pin only while porting
the full corresponding overlay. Protocol contracts remain in the Protocol
repository and are not duplicated in these bundles.

| Bundle | Responsibility |
|---|---|
| 2000 | Gateway sessions, characters, persistence and transfers |
| 2001 | NPC population, identity, checkpoints and handoffs |
| 2002 | Server lifecycle, operations, permissions and FLHookCompat |
| 2003 | Rendering, interface, materials, animation and THN |
| 2004 | Managed builds, shader toolchain, project and CI integration |
| 2005 | Dynamic market trades and trader routing |
| 2006 | Gameplay and remaining upstream compatibility fixes |
| 2007 | NAP assets, packaging tools and editor workspace |
| 2008 | Audio decoder integration and room playlists |
| 2009 | Client and asset regression tests with synthetic fixtures |
| 2010 | Project findings, TODOs and graphics implementation plan |
| 2011 (`lancerdecode`) | Native Vorbis EOF/reset handling |
| 2012 (`blurgtext`) | System FreeType without OT-SVG hooks |

Shared integration files may contain several related features; their bundle
owns the complete file delta. Bundles apply in `series` order. Ordinary feature
work may use a focused follow-up patch or regenerate the affected bundle after
clean reconstruction; preserve unrelated deltas and synthetic fixtures.

## Consolidation evidence

`consolidation-2026-10-12.json` records the pinned baselines, previous active
series, preserved documentation history and file hashes before bundling.
The old patch files remain available in Git history at the recorded Client
baseline. Historical patch numbers in runtime reports refer to that history.
The consolidation reduced 192 active patches to 13 and carried forward the
local documentation findings omitted by the previous main-port series.

Run `python3 scripts/verify-lancer-nexus-overlay.py` to reconstruct and compare
the complete overlay. `scripts/tests/test_verify_overlay.py` checks routing,
pinned baselines, drift rejection and the patch-state recorder. The normal
build applier records only a verified complete overlay; avoid manually copying
source files between branches.

The operator-authorized `/clusterjump <system> <source gate>` implementation
is part of the server operations bundle. It still requires
`command.clusterjump`, Gateway/Coordinator authorization and a reciprocal
jump-gate route.
