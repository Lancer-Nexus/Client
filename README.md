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
