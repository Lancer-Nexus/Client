# LancerEdit NAP Workspace

When LancerEdit starts from a Client release with `packages/active.json`, it mounts the same active package snapshot above the selected Freelancer installation. Package-backed edits are written to a separate persistent workspace, not into the NAP archive or the selected installation.

The workspace path is logged at data-load time. It is stored below `<LocalApplicationData>/LancerEdit/NAPWorkspace/<install-id>` (or the application-local fallback when no local application-data directory is available). Files already present in the workspace override both NAP packages and loose DATA files. Saves for packaged files and directories resolve to workspace paths; loose files that are not shadowed by a package keep their original backing paths.

To export the effective package content plus workspace edits as a new full NAP:

```bash
nexus-pack compact <client-release-directory> <output.nap> --workspace <logged-workspace-directory> --version <content-version>
```

The output must be outside both the Client release and workspace. Compaction verifies the resulting NAP but does not sign a release manifest or include loose DATA fallback files. Review the package identity, mount order and override relationships, then update and sign the release manifest through the normal publishing process. Keep the workspace until the exported package has been validated and activated.

The workspace is local editor state. It is not loaded by the game client or LLServer and is not included in the signed active snapshot automatically.
