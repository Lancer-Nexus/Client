# InterfaceEdit NAP Integration Plan

## Current state

`InterfaceEdit/Project.cs::Load` creates `FileSystem.FromPath(FlFolder)` and then constructs `GameResourceManager`, `FreelancerIni`, fonts, rollover/HUD data, and other interface resources from that VFS. Patch 2117 now mounts the verified `packages/active.json` snapshot and shared editor workspace before constructing game resources. Assets present only in active NAP packages are therefore available in InterfaceEdit.

The shared `LibreLancer.Data.NexusEditorDataMount` helper mounts the active snapshot and persistent workspace for both LancerEdit and InterfaceEdit. It retains LancerEdit’s existing workspace identity/location so existing edits stay visible. NAP pack/unpack/export remain in LancerEdit/CLI.

`InterfaceEdit/ResourceWindow.cs` saves Freelancer's `resources.xml` through `FileSystem.GetBackingFileName`. By contrast, project XML/Lua files and `Project.WriteResources()` are stored under the InterfaceEdit project folder. The integration preserves that distinction: `resources.xml` uses the writable VFS path, while project XML/Lua files and `Project.WriteResources()` remain in the project folder.

## Intended behavior

- If an active package snapshot exists beside the InterfaceEdit executable, InterfaceEdit reads the same verified package snapshot as Client and LancerEdit.
- The selected Freelancer directory remains the lowest-priority loose-data fallback. With no active snapshot, the existing loose-data workflow is unchanged.
- Writes to files supplied by a NAP go to a persistent loose workspace above the package layers. Never modify a mounted NAP in place.
- InterfaceEdit project files remain in the project folder; only game-data resources use the NAP workspace.
- Invalid active snapshot or package integrity errors are surfaced to the user and prevent silently loading a partial package set.

## Implementation status

Implemented in patches 2117–2119: common mount helper; InterfaceEdit snapshot/workspace mounting before resource loading; workspace-backed `resources.xml` path resolution with an explicit error when no writable backing path exists; four synthetic assertions across three tests for package precedence, workspace persistence after refresh and remount, loose-only mode, and rejection of a corrupt snapshot without changing the VFS. Linux Release build and focused tests passed.

Still open: GUI save/restart smoke test, Windows build/test/runtime, GUI verification of corrupt-snapshot error presentation, broader OS-specific backing-path audit, and expanded edge-case coverage (traversal/reparse points and pre-existing workspace fixture).

## Implementation sequence

1. **Share package mount setup — implemented.** `NexusEditorDataMount.Attach` accepts the application directory, selected game-data directory and optional workspace base, and attaches package/workspace providers above loose DATA. Both editors use it.
2. **Share workspace identity — implemented with compatibility preservation.** The helper retains the existing LancerEdit workspace root and install ID, so existing workspace edits remain visible without copying or moving files. Cross-platform case behavior still needs Windows verification.
3. **Mount packages in InterfaceEdit — implemented.** `Project.Load` calls the helper immediately after creating the loose-data VFS and before game-resource initialization. Loose-only mode remains supported.
4. **Route game-data writes through the workspace — partially implemented.** `resources.xml` resolves its writable path through the VFS and fails clearly when unavailable. Project files remain project-local. Audit other OS backing-path consumers and interactive saves before calling this complete.
5. **Expose mount state — implemented.** The Data menu shows whether the active snapshot is mounted and the workspace path; it can open the workspace using the desktop file manager. Open failures are shown in the editor. Snapshot load and resource-save failures are caught and shown in the editor; verify the corrupt-snapshot flow in a GUI session.
6. **Add synthetic integration coverage — partial.** Tests cover NAP-over-loose precedence, workspace destination, persistence after refresh, and loose-only mode. Traversal/reparse points and a separately pre-seeded legacy workspace fixture remain to add or verify.
7. **Verify platforms/editor path — partial.** The focused tests and Release build pass on Linux; a fresh patch-series reconstruction applies through patch 2117. Windows tests/build, GUI save/restart smoke test and a fresh interactive InterfaceEdit session remain.

## Boundaries

This plan adds NAP-backed reading and safe workspace editing to InterfaceEdit. NAP pack, unpack, compact/export and release signing remain the existing LancerEdit, CLI, and publisher responsibilities. Tests use synthetic assets only; original Freelancer data is not committed or distributed.
