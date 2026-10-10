# InterfaceEdit NAP Integration Plan

## Current state

`InterfaceEdit/Project.cs::Load` currently creates `FileSystem.FromPath(FlFolder)` and constructs game resources from loose data. Patches 2117–2120 temporarily added active-snapshot mounting, workspace routing, status UI and recovery tests. Patch 2121 removes those InterfaceEdit changes from the current overlay while aligning the patch series with the rebased upstream tree. The prior GUI smoke is historical evidence for the pre-2121 overlay, not the current application.

LancerEdit remains the supported NAP editor workflow: it mounts the active snapshot and persistent workspace and provides pack, unpack and export actions. InterfaceEdit integration is deferred until the current overlay builds cleanly and its required write paths can be verified.

`InterfaceEdit/ResourceWindow.cs` currently saves Freelancer's `resources.xml` through the loose-data backing path. If NAP mounting is restored, `resources.xml` must resolve through the writable workspace; project XML/Lua files and `Project.WriteResources()` should remain in the InterfaceEdit project folder.

## Intended behavior

- If an active package snapshot exists beside the InterfaceEdit executable, InterfaceEdit reads the same verified package snapshot as Client and LancerEdit.
- The selected Freelancer directory remains the lowest-priority loose-data fallback. With no active snapshot, the existing loose-data workflow is unchanged.
- Writes to files supplied by a NAP go to a persistent loose workspace above the package layers. Never modify a mounted NAP in place.
- InterfaceEdit project files remain in the project folder; only game-data resources use the NAP workspace.
- Invalid active snapshot or package integrity errors are surfaced to the user and prevent silently loading a partial package set.

## Implementation status

Patches 2117–2120 implemented and verified this behavior on the pre-alignment overlay. Patch 2121 currently removes the InterfaceEdit integration and its tests. No active-snapshot mount or NAP workspace save path is present in the current InterfaceEdit source.

Still open: GUI save/restart smoke test, Windows build/test/runtime, GUI save/restart roundtrip and GUI verification of corrupt-snapshot error presentation, broader OS-specific backing-path audit, and expanded edge-case coverage (traversal/reparse points and pre-existing workspace fixture).

## Implementation sequence

1. **Share package mount setup — planned.** Reuse LancerEdit’s active-snapshot validation and persistent workspace without changing its identity or location.
2. **Share workspace identity — planned.** Preserve existing LancerEdit workspace edits; verify case handling on Windows.
3. **Mount packages in InterfaceEdit — deferred.** Add the mount before game-resource loading after the current patch stack builds cleanly.
4. **Route game-data writes through the workspace — planned.** Resolve `resources.xml` to a checked writable path; keep project files project-local.
5. **Expose mount state — planned.** Show active snapshot/workspace status and report mount/save failures.
6. **Add synthetic integration coverage — planned.** Cover package precedence, persistence after refresh/remount, loose-only mode, corrupt snapshots, traversal and reparse points.
7. **Verify platforms/editor path — pending.** The current clean overlay applies through patch 2121 and the NAP asset suite passes 28/28. InterfaceEdit-specific build/runtime verification is not current evidence; the LancerEdit Release build is blocked by unrelated missing Protocol/client types. Windows and GUI save/restart checks remain open.

## Boundaries

This plan adds NAP-backed reading and safe workspace editing to InterfaceEdit. NAP pack, unpack, compact/export and release signing remain the existing LancerEdit, CLI, and publisher responsibilities. Tests use synthetic assets only; original Freelancer data is not committed or distributed.
