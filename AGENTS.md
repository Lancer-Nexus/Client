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
- For local Debug E2E runs, start the client with `SSL_CERT_FILE=/path/to/test-ca.pem output/dev/client/run.sh /path/to/client.ini --credentials-file=/path/to/private-test-account.txt [--character=PilotName]`. The first positional argument is the client configuration; `--credentials-file` starts Gateway login automatically. The credentials file contains `Email: ...` and `Password: ...` lines and must have mode 0600. `--character` automatically selects an existing character after login; it does not create one. For self-signed local Gateway certificates, `SSL_CERT_FILE` must point to the test CA. Keep credentials out of process arguments, logs, commands, commits and captured output. These options are Debug-only and must not be used for Release builds.
- Treat Gateway responses and transfer tickets as untrusted input and validate them.
- Do not let the client decide authoritative placement, ownership, credits or inventory.
- Preserve the existing game simulation unless a change is explicitly part of the MMO integration.
- Transferred population groups must keep their world active without players until they dock, die or transfer again. Player-distance culling must not discard these leased NPCs; do not generate extra ambient population merely to keep an unobserved world active. Retain source worlds while population handoffs are pending.
- Publish the private NPC listener separately in runtime status; use the Coordinator's NpcTransferEndpoint and its explicit port for initial send and recovery. Only legacy peers without that field use the configured port fallback.
- Resolve source jumpers by stable NPC ID rather than nickname. New population nicknames must not collide across world restarts or imported groups. Keep npc-state diagnostics local and Debug-only, and capture them on the simulation queue.
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

Cluster `/admin` chat must be intercepted before logging/broadcast, including styled fragments. Keep replies private. Gateway admission supplies account/session or transfer identity server-side; current Gateway leases and Administration SQL roles/scopes authorize access, never local admin flags. Shared query contracts belong in Protocol; maintain engine changes only in patches.

Permission changes use typed Gateway requests and are authorized centrally by Administration. Do not use LLServer's legacy admin/deadmin flags as clustered permissions. The local UUID operator fallback is console-managed and only active when Gateway/cluster login is disabled; it must never bypass Nexus authorization. All related LibreLancer source changes remain patch-only.

Every player-facing server command that is currently guarded by `Character.Admin` must receive its own `command.<name>` permission and be checked fail-closed through Gateway with the account, session, character lease, instance and system context before execution. Keep the command-to-permission catalog in [Administration's permission guide](../Administration/docs/permission-system.md#permissions-fuer-bestehende-librelancer-adminbefehle). Do not silently retain the legacy flag as a clustered fallback; standalone OP fallback must remain explicitly isolated.

See [the Administration permission system guide](../Administration/docs/permission-system.md) for the command syntax, local operator boundaries, and the fact that LLServer revision-sync startup is still pending.

1. Identify whether the change belongs in the client or in Gateway/Protocol/Cluster.
2. Keep the smallest possible patch against the upstream fork.
3. Build the client and run the relevant UI and protocol tests.
4. Document any upstream conflict or compatibility assumption.
5. Keep every client change in the patch series under `patches/series`; the patch applier runs before the supported build entrypoints and skips patches already applied. Patch files are the maintained implementation; applied source changes are build workspace state only.

## Nexus baseline system groups

The base Nexus topology uses eight game instances, one per group: BR01-BR06 (`br-01`), BW01-BW10 (`bw-01`), EW01-EW05 (`ew-01`), IW01-IW06 (`iw-01`), KU01-KU06 (`ku-01`), LI01-LI05 (`li-01`), RH01-RH05 (`rh-01`), and `mixed-01` for all remaining registered systems. System nicknames are compared case insensitively and emitted lowercase. Folder names are not always world nicknames: `fp7` contains `fp7_system`; `intro` and `miners` are asset directories, not registered worlds.
LLServer SystemIds defines owned worlds; SystemId is the primary fallback. Travel within the owned set remains a local system jump; travel to another group uses the fenced Gateway transfer. Runtime status must publish the entire set. The new BindAddress setting binds the game listener privately; cluster servers do not open the legacy discovery listener. Keep these changes patch-only.

## NPC retirement integration

- Queue retirement through the durable background outbox; do not perform file or
  HTTP I/O on the simulation thread. Await durable intent and inspect every
  Coordinator entry result before treating retirement as confirmed.
- Keep pending and confirmed original ownership fences as restore barriers. A
  corrupt outbox must prevent recovery readiness; never discard it on timeout.
- Do not wire terminal hooks through generic object removal: transfer freezing
  also removes objects and must never retire their identities.
- Death of an identified ambient NPC must keep the NPC and its group reserved
  until the atomic survivor-checkpoint plus retirement write is accepted. Block
  population transfers and player-distance culling for that NPC while pending.
- MissionRuntime terminal events remain excluded until Gateway character-lease
  arbitration is implemented. Death and terminal docking now wait for a committed
  group checkpoint before removal. Restart-time checkpoint restoration remains
  required lifecycle work.

## Overlay integrity

- Before recording a manually applied overlay, reconstruct and compare the full
  series with `python3 scripts/verify-lancer-nexus-overlay.py`. The record command
  enforces this check and requires Python 3. Only CRLF/LF differences are ignored.
- A GUI compilation failure involving removed admin types can indicate unapplied
  patch 1098. Inspect the local overlay against a fresh reconstruction before
  adding replacement contracts or another patch. Patch 1097 also restores failure
  propagation in the build entry point.
- The integrity checker is read-only; preserve unrelated modifications and repair
  identified overlay drift by applying the relevant maintained patch.

- GameServer writes periodic checkpoints for active stable-ID ambient NPC groups
  from the simulation queue, then persists and delivers them through the bounded
  background outbox. Keep serialization capture on the simulation queue and all
  staging/network I/O on outbox workers. Do not include MissionRuntime until
  Gateway lease arbitration is wired. Terminal death/dock must use a survivor
  checkpoint plus retirement as one write; do not hook generic removals.
