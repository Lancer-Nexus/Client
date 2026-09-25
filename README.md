# Lancer Nexus Client

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

When `LoginUrl` points at the Gateway, the existing LLServer connection challenge verifies the client's short-lived `JoinTicket` through `POST /api/v1/game/verify-ticket`; legacy `/verifytoken` servers remain supported as a fallback. The ticket must be obtained from Gateway placement and is never logged or stored in client configuration.

The LLServer core exposes `GameServer.StageCharacterTransferSnapshotAsync` for a transfer already reserved through Gateway. It freezes the connected character, uploads the `.fl` snapshot to Gateway over HTTPS, and waits for Gateway to confirm durable staging before reporting `SourceFrozen`. `GameServer.DownloadTransferSnapshotAsync` lets the authenticated target instance retrieve that staged snapshot. Both methods use `InstanceId` from server configuration and the per-instance bearer key in `LANCER_NEXUS_GAME_INSTANCE_KEY`; the key is never written to JSON or logs. A timeout does not resume the source because Gateway may have completed the state transition before the response was lost; the caller must check authoritative transfer status. `AbortCharacterTransfer` remains restricted to a confirmed abort, and `ReleaseCharacterAfterTransferCommit` to a confirmed atomic lease commit. Snapshot import into the target's local character database, the client-visible safe-point trigger, and automatic lifecycle polling remain open, so jump-gate travel is not end-to-end ready.
