# NAP Overlay Patch Builder

`nexus-pack diff <base.nap> <target.nap> <patch.nap> <base-package-id>` verifies both source archives, compares normalized case-insensitive paths and complete file hashes, and emits only added/changed files plus tombstones for deletions. It also writes `<patch.nap>.plan.json` with the base package dependency/override declaration. Review that plan and merge it into the signed release manifest; the sidecar is advisory and is not signed.

The base ID must identify the package layer represented by `<base.nap>`. The patch is mounted after that layer and its manifest entry must declare the listed dependency and override. If the base snapshot contains multiple packages, build against the effective package state and declare all package relationships needed by the signed release plan; the simple CLI command takes one base ID.

`nexus-pack compact <application-dir> <output.nap> [--version N] [--workspace <dir>]` loads `packages/active.json`, compacts the effective NAP package tree into a new full package, removes obsolete layers and tombstones, and verifies the result. If supplied, `--workspace` overlays persistent LancerEdit changes above the active packages. The output must be outside the active application and workspace directories. Compaction does not include loose DATA fallback files or sign/activate a release; update the signed manifest through the publisher workflow.

Both commands use temporary files for source materialization. `diff` preserves the target archive content version. Compaction defaults to content version 1 unless `--version` is supplied. Neither command creates `.napd` transport deltas.
