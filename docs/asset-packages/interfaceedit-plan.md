# InterfaceEdit NAP Integration Plan

## Current state

`InterfaceEdit/Project.cs::Load` creates `FileSystem.FromPath(FlFolder)` and then constructs `GameResourceManager`, `FreelancerIni`, fonts, rollover/HUD data, and other interface resources from that VFS. It does not mount `packages/active.json` or an editor workspace. This means InterfaceEdit cannot currently read assets that exist only in an active NAP snapshot.

The separate `LancerEdit` path already mounts `NexusPackageFileProvider.LoadActive(AppContext.BaseDirectory)` and puts `NexusPackageWorkspaceFileProvider` above it. Package-backed file paths resolve to persistent workspace files. Its Data menu already handles NAP pack/unpack/export; those operations should remain in LancerEdit/CLI unless InterfaceEdit develops a specific package-authoring workflow.

`InterfaceEdit/ResourceWindow.cs` saves Freelancer's `resources.xml` through `FileSystem.GetBackingFileName`. By contrast, project XML/Lua files and `Project.WriteResources()` are stored under the InterfaceEdit project folder. The integration must preserve that distinction.

## Intended behavior

- If an active package snapshot exists beside the InterfaceEdit executable, InterfaceEdit reads the same verified package snapshot as Client and LancerEdit.
- The selected Freelancer directory remains the lowest-priority loose-data fallback. With no active snapshot, the existing loose-data workflow is unchanged.
- Writes to files supplied by a NAP go to a persistent loose workspace above the package layers. Never modify a mounted NAP in place.
- InterfaceEdit project files remain in the project folder; only game-data resources use the NAP workspace.
- Invalid active snapshot or package integrity errors are surfaced to the user and prevent silently loading a partial package set.

## Implementation sequence

1. **Share package mount setup.** Extract the active-snapshot and workspace-provider setup used by LancerEdit into a small reusable editor integration helper. It should accept an application directory, selected game-data directory, and workspace root, and return the configured VFS plus mount/workspace metadata. Both editors must retain the order `loose DATA -> active packages -> workspace`.
2. **Define one workspace identity and migrate safely.** Use the canonical full Freelancer installation path to derive a stable ID, with case handling that works on Windows and Linux. Move the current LancerEdit-only workspace convention to a shared editor namespace; detect existing LancerEdit workspaces and preserve them or provide a non-destructive migration. Do not silently start a second empty workspace and hide prior edits.
3. **Mount packages in InterfaceEdit.** Configure `UiData.FileSystem` through the shared helper before creating `GameResourceManager`, `FreelancerIni`, fonts, HUD and rollover resources. Keep `ProjectConfiguration.DataFolder` semantics intact and continue to allow ordinary loose installations.
4. **Route game-data writes through the workspace.** Audit all InterfaceEdit writes. Resolve `resources.xml` using the VFS writable backing path and report a clear error if a packaged path has no writable destination. Keep project-local stylesheet, XML/Lua editor files, generated designer files and compiler output in their existing project/output directories. Do not redirect them into the game-data workspace.
5. **Expose mount state.** Show whether an active NAP snapshot was loaded and where the shared workspace lives, with a way to open the workspace directory. Report snapshot validation errors instead of silently falling back to loose DATA when a snapshot was present but invalid.
6. **Add synthetic integration coverage.** Build a tiny NAP containing an interface resource and a referenced asset. Verify InterfaceEdit's configured VFS resolves it from NAP with loose DATA absent; verify saving a packaged resource creates/updates only its workspace copy and a reload sees the workspace override. Also cover loose-only mode, corrupt active snapshots, traversal/reparse-point rejection, and preservation of pre-existing LancerEdit workspace files.
7. **Verify both platforms and the editor path.** Build InterfaceEdit on Linux and Windows, run the synthetic tests on both, and perform a GUI smoke test that opens a project against a NAP-only fixture, edits/saves a packaged resource, restarts, and sees the saved override. Keep all LibreLancer source changes in a focused patch registered in `patches/series`; validate the patch stack from a clean reconstruction.

## Boundaries

This plan adds NAP-backed reading and safe workspace editing to InterfaceEdit. NAP pack, unpack, compact/export and release signing remain the existing LancerEdit, CLI, and publisher responsibilities. Tests use synthetic assets only; original Freelancer data is not committed or distributed.
