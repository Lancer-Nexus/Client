# Lancer Nexus Client

## Private cluster administration chat

On cluster servers, `/admin help`, `/admin status`, `/admin instances` and `/admin instance <ID>` query the central Administration service through Gateway. Replies are private console messages; complete fragmented chat is intercepted before normal chat logging or broadcast. Rights come exclusively from current Administration SQL roles/scopes and the active Gateway session/character lease. Admission retains the Gateway session or committed transfer ID server-side. Standalone/local admin flags cannot grant access. One query per player may be outstanding; allow two seconds between requests. Engine changes are maintained in patches 1040/1041 and applied before builds.

The Lancer Nexus Client is the MMO-oriented client fork of [LibreLancer](https://github.com/Librelancer/Librelancer).

## Scope

- Provide the game client and its existing LibreLancer functionality.
- Add the Lancer Nexus login flow with e-mail and password.
- Connect players through the Gateway instead of exposing a direct server list in production.
- Display instance information and support controlled transfers between instances.
- Keep the upstream LibreLancer integration surface as small and reviewable as possible.

## Development

The client targets the current .NET version used by the upstream project. Cluster features must remain optional behind configuration or capability negotiation so that standalone development and upstream synchronization continue to work.

## Related repositories

- `Gateway` – authentication, session and routing entry point
- `Protocol` – shared wire contracts
- `Cluster` – shared client/server abstractions

See `AGENTS.md` for contribution and integration rules.

## Shared Protocol

The shared contracts are checked out in the `Protocol` submodule. Update it before MMO/client builds with:

```bash
git submodule update --init --remote --merge Protocol
```

CI performs the same update before the existing LibreLancer build. Standalone client builds remain possible when the cluster integration is disabled.

Lancer Nexus client changes are carried by the patch series in `patches/series`. The root build orchestrator applies it before building; direct `./build.sh` runs the same idempotent patch applier when the overlay is present. The applier rejects partial or conflicting patch states instead of forcing them.

The `LibreLancer` project references the shared `Protocol` submodule for its optional Gateway version exchange. Set `cluster_gateway_url` to a trusted HTTPS Gateway in `[Librelancer]` to enter the Gateway login screen at startup; `cluster_target_system` (default `li01`) and `cluster_region` (default `eu`) control the first placement request. An empty Gateway URL keeps the standalone menu and server discovery.

The Lancer Nexus collector stages Linux client builds with the `lancer` apphost and its dependencies at the client root, with Freelancer `DATA` contents under `data/`. Its generated root `librelancer.ini` points to that directory. The package keeps only the resource DLLs, `content.dll`, and the two `.fl` start files used by LibreLancer; the original Freelancer and server executables are not required. LibreLancer still accepts the conventional `EXE/` and `DLLS/BIN/` paths for existing installations.

`BuildLL` packages Linux and Windows engine and SDK releases as one-root-directory `tar.zst` archives. The packager preserves Unix modes and fails if a release tree contains symbolic links, which are not accepted by the updater's safe extractor.

`NexusGatewayLogin` reads `client-version.json` from the running client release directory, POSTs `ClientVersionHello` before credentials, then requests placement with the authenticated session. The client connects to the assigned endpoint with the Gateway JoinTicket. A required update displays a prompt and exits with code 42 after acknowledgement. Missing or damaged local version metadata requests repair with exit code 43. The updater can stage and atomically activate a verified client-only release, then launch its `lancer` executable. Required data packages block activation until their installer is implemented; automatic rollback after an unhealthy startup remains pending.

## Dedicated server runtime status

`LLServer` can optionally publish an atomic JSON runtime snapshot for the host Agent. Set `RuntimeStatusFile`, `InstanceId`, `SystemId`, `InstanceEndpoint`, and `MaxPlayers` in its server configuration. `MaxPlayers` controls the listener limit and must match the Agent's `Agent__Instance__MaxPlayers`. The snapshot reports the actual listener running state and connected peers; it contains no credentials. Optionally set `DrainFlagFile` to a host-local path; while that file exists, the snapshot reports `IsDraining=true`, and the Coordinator stops assigning new sessions while existing sessions remain connected. The host lifecycle controller can enter or leave drain by creating or removing the flag file. Leave these fields unset for standalone deployments. The Agent treats missing or stale snapshots as not ready; LLServer removes the snapshot on a clean stop.

Cluster LLServer instances use `NpcCoordinatorUrl` plus the environment variable `LANCER_NEXUS_COORDINATOR_API_KEY` for Coordinator-issued NPC identities. Keep the key out of `llserver.json`. A single background worker per hosted system keeps a block of 128 IDs buffered and asks for another block below 32. The game simulation only takes an ID from a local queue; when the pool is empty, the NPC remains pending and is inserted into the world after the Coordinator replies. Network delays therefore do not block the game loop. The Coordinator's MySQL NPC ownership migration must be applied before startup.

NPC runtime snapshot schema 4 preserves formation membership using stable NPC/character references, the leader, member offsets and the player's formation targets. The source rejects a formation that is not fully included in the transfer; the target resolves all members before activating the restored group.

Transferred population traders and escorts keep their simulation world alive without players and are not removed by player-distance culling. Existing routes continue in unobserved worlds; ambient repopulation still requires players. A pending population handoff retains its source world until send/commit or rollback finishes. Rollback re-adopts restored ships into trader traffic and excludes the failed gate for the next route. Runtime validation preserves finite formation catch-up throttle and world-space steering values exactly, including values above one.

LLServer runtime status advertises its private `NpcTransferEndpoint` separately
from its game endpoint. The Coordinator returns this endpoint for both initial
handoffs and recovery; senders preserve the advertised QUIC port. Configure
distinct NPC transfer ports for multiple LLServer processes on the same host.

Source freezes resolve NPCs by their Coordinator-issued IDs, including mission
groups. Population names have a per-world namespace so imported names cannot
collide with new encounter spawns. Debug LLServer's local console command
`npc-state [NPC UUID]` captures active IDs, ownership versions, positions and
formation leaders between simulation updates; it exposes no network endpoint.

Each NPC runtime payload also preserves transform and velocity, health, equipment and shield state, cargo, AI graph timers and random state, and autopilot targets through stable NPC/character/world references. Goto, dock and undock autopilots retain their active phase, including docking-ring and tradelane-entry progress. The source writes the captured snapshot durably on the simulation thread before removing the NPCs; the target restores the full group inert and exposes it only after Coordinator ownership commit.

The inbound NPC peer receiver is disabled when `NpcTransferPort` is `0`. To enable it, configure `NpcTransferListenAddress` with a private bind address, `NpcTransferPort`, `NpcTransferServerCertificate` (PFX with the instance DNS SAN), `NpcTransferClientCaCertificate` (PEM CA), and `NpcTransferStagingDirectory`. Set the PFX password through `LANCER_NEXUS_NPC_TRANSFER_CERT_PASSWORD`; do not store it in `llserver.json`. The receiver stages authenticated snapshots durably and reports `TargetAccepted` to the Coordinator. Mission snapshot schema 4 preserves active/completed triggers, trigger timers and flags, condition storage, per-label spawned/alive/destroyed objects, the deterministic random stream, current objective, save trigger, pending communication lines and generated random-mission parameters. The target reconstructs generated scripts from stable data nicknames and the captured parameters, then restores the active mission offer. Before capturing, the source drains queued mission world actions on the simulation thread; it captures before transfer exit/enter events, and the target replays those events after both character and NPC ownership are committed. The imported savegame initializes the target player mission, then the NPC group and player spawn together in one world-thread action. A write-through activation receipt prevents an already activated stage file from replaying after restart; recovery waits for the associated player to enter its world before activating NPCs. The source keeps a durable rollback snapshot until the handoff resolves. Unsupported NPC or condition states are rejected.

When `LoginUrl` points at the Gateway, the existing LLServer connection challenge verifies the client's short-lived `JoinTicket` through `POST /api/v1/game/verify-ticket`; legacy `/verifytoken` servers remain supported as a fallback. The ticket must be obtained from Gateway placement and is never logged or stored in client configuration.

The LLServer core exposes `GameServer.StageCharacterTransferSnapshotAsync`, `GameServer.ImportTransferSnapshotAndAcceptAsync`, and `GameServer.ResolveSourceTransferAsync`. The source freezes the connected character, uploads the `.fl` snapshot over HTTPS, and waits until Gateway durably stages it before the transfer becomes `SourceFrozen`. The authenticated target verifies its transfer ticket, records an idempotency receipt in its local SQLite database, imports the save and then asks Gateway to accept and commit. The source resolver checks Gateway's committed lease version before discarding the frozen player and announcing `SourceReleased`; aborted or expired transfers resume the source. LLServer polls pending source transfers automatically when `InstanceId` and `LANCER_NEXUS_GAME_INSTANCE_KEY` are configured. The local receipt and character replacement share one SQLite transaction; retries require the same ticket and snapshot. Both instances use their configured `InstanceId` and the per-instance bearer key in `LANCER_NEXUS_GAME_INSTANCE_KEY`; the key is never written to JSON or logs. The transfer ticket replay check occurs before the durable receipt is written, so a process crash in that narrow interval requires the transfer to be aborted and retried. Cross-system jump gates now reserve a target through Gateway, stage the frozen source snapshot with destination-system arrival data, reconnect with a transfer ticket, and invoke the target importer before automatically selecting the transferred character. The client changes remain patch-only in `patches/series`.

The transfer status lease version is populated at commit. Before commit, the target retains the source lease version from the Gateway-verified transfer ticket; Gateway checks that version against the authoritative MySQL lease during acceptance. Targets must not compare the source version with the unset pre-commit status version.

The active series uses consolidated client and Protocol overlays to reproduce the verified development build from the committed fork baseline. Earlier focused patches remain in `patches/` as history; do not apply them a second time. Documentation and the patch build hook are already tracked in the baseline.

LLServer accepts `SystemIds` to host a Nexus system group with one database, player limit and endpoint. `SystemId` remains the primary fallback. Jumps within the set stay local; jumps outside use Gateway transfer. `BindAddress` can restrict the UDP listener to a private IPv4 interface, and cluster mode disables the legacy discovery listener. The eight-group baseline is generated by Scripts after collection.

## NPC retirement recovery infrastructure

Patch 1126 adds a durable background retirement outbox under the configured NPC
transfer staging directory's `retirements` subdirectory. Startup loads pending and
confirmed records before recovery; matching NPC fences block snapshot activation.
The GameServer API exposes separate persistence and Coordinator-response tasks.
Retirement requires Coordinator migration 003 and `npc_retirement_v1` support.
Death/docking hooks and survivor/MissionRuntime checkpoints remain pending; the
outbox alone does not establish complete NPC lifecycle recovery. See
[the verification scope](docs/npc-mission-transfer-verification.md).
