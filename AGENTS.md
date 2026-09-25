# AGENTS.md – Lancer Nexus Client

## Mission

Maintain the Lancer Nexus client fork while preserving compatibility with upstream LibreLancer wherever possible.

## MVP architecture baseline

- The client authenticates only through Gateway and never owns placement, leases, credits, inventory or other authoritative character state.
- It treats Gateway assignments and short-lived, single-use transfer tickets as untrusted input and follows the transfer lifecycle `Requested -> Reserved -> Prepared -> SourceFrozen -> TargetAccepted -> Committed -> SourceReleased` without returning to the main menu.
- The active game instance remains authoritative until the MySQL-backed lease changes atomically at `Committed`; the client must handle rejection, expiry and recovery states.
- Shared, versioned MessagePack contracts and capabilities belong in `Protocol`; Redis is never exposed to the client.

## Rules

- Keep the LibreLancer fork baseline untouched while authoring Nexus work. Store every Nexus client or engine modification only in a focused patch under `patches/`, register it in `patches/series`, and let the build patch applier apply it immediately before compilation.
- Do not make direct edits to fork source files for Nexus features or fixes. To inspect or test an applied overlay, derive it from the patch series and keep the patch files as the only maintained source of those changes.
- Do not rewrite or reformat unrelated upstream code.
- Keep cluster functionality disabled by default for standalone builds.
- Put shared wire contracts in `Protocol`; do not duplicate MessagePack models here.
- Never store passwords, refresh tokens or private keys in logs or client configuration.
- Treat Gateway responses and transfer tickets as untrusted input and validate them.
- Do not let the client decide authoritative placement, ownership, credits or inventory.
- Preserve the existing game simulation unless a change is explicitly part of the MMO integration.
- Add tests for login state, reconnect, instance display and transfer failure paths.

## Existing LibreLancer build and test model

- This repository is the existing LibreLancer fork, not an empty .NET service repository. Preserve its upstream layout, submodules and native dependencies.
- The supported entry point is `./build.sh`, which validates `build.config`, checks native dependencies and submodules, then runs `scripts/BuildLL/BuildLL.csproj`.
- `BuildLL` emits the Windows and Linux engine/SDK release archives as single-root `tar.zst` packages; the packager uses the .NET Zstandard stream and preserves Linux executable modes.
- The main solution is `LibreLancer.sln`; it contains the client, server, editor, launcher, tests and native-integrated projects. Do not invent a second solution or packaging path for MMO work.
- The primary automated test project is `src/LibreLancer.Tests/LibreLancer.Tests.csproj`. Run the relevant test subset after client changes; use the full solution build when the change crosses shared engine or project boundaries.
- Native components are configured through the existing `CMakeLists.txt` and dependencies checked by `scripts/depcheck_unix`; do not replace this with a service-style `dotnet restore`-only workflow.
- The current project files target `net10.0`. Keep cluster functionality optional and isolated behind existing configuration/capability boundaries while upstream synchronization remains possible.
- `src/LibreLancer/LibreLancer.csproj` references the shared `Protocol` submodule for the optional Gateway version handshake; initialize that submodule before building the client.

## Working-model escalation

- If a task requires complex reasoning beyond the current model's reliable scope, ask the user whether switching to a stronger model is desired before continuing.
- Do not switch models silently or broaden the task because a stronger model may be useful.

## Change process

1. Identify whether the change belongs in the client or in Gateway/Protocol/Cluster.
2. Keep the smallest possible patch against the upstream fork.
3. Build the client and run the relevant UI and protocol tests.
4. Document any upstream conflict or compatibility assumption.
5. Keep every client change in the patch series under `patches/series`; the patch applier runs before the supported build entrypoints and skips patches already applied. Patch files are the maintained implementation; applied source changes are build workspace state only.
