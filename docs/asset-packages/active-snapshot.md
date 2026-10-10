# Local NAP Active Snapshot

The Client and LLServer look for `packages/active.json` below their application directory. If the file is absent, the package provider is not added and the existing loose DATA/overlay behavior remains in effect. If it exists but is malformed or references invalid packages, startup fails closed.

V1 snapshot shape:

```json
{
  "schemaVersion": 1,
  "manifestId": "data-release-id",
  "packages": [
    {
      "id": "core",
      "version": 3,
      "path": "core/game-config.nap",
      "size": 1234567,
      "sha256": "<64 lowercase hex characters>",
      "required": true,
      "priority": 100,
      "mountOrder": 1,
      "dependencies": [],
      "overrides": []
    }
  ]
}
```

Package paths are relative to `packages/`; traversal, rooted paths, reparse points, mismatched sizes and SHA-256 values are rejected. NAP content and its index are checked when opened. Packages are ordered by `(priority ASC, mountOrder ASC, packageId ASC)`. Two packages at the same priority must use different mount orders. A later package that defines an already-defined virtual path must name the currently winning package ID in `overrides`; tombstones follow the same conflict rule.

`active.json` is an installation snapshot, not the signed release manifest. The runtime validates its shape and every referenced package digest, but does not authenticate the snapshot itself. The updater validates the signed manifest and each NAP V1 structure, chunk and file hash before writing this snapshot into a staging release, then atomically activates the client and data release together. A Linux smoke test using a full game-data container with loose `DATA` absent reached the first UI state on 2026-10-09. Runtime rollback after an unhealthy start remains pending. Do not treat a manually edited snapshot as a trusted release decision.

The Client and LLServer mount this provider above their existing loose DATA/overlay providers. VFS lookup masks fallback files for package overrides and tombstones. File and directory enumeration reports only effective visible entries. `GetBackingFileName()` returns no physical path for archived files. LancerEdit does not mount active packages yet, because its save operations require a deliberate materialization/export strategy.
