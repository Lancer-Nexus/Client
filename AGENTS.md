# AGENTS.md – Lancer Nexus Client

## Mission

Maintain the Lancer Nexus client fork while preserving compatibility with upstream LibreLancer wherever possible.

## Rules

- Do not rewrite or reformat unrelated upstream code.
- Keep cluster functionality disabled by default for standalone builds.
- Put shared wire contracts in `Protocol`; do not duplicate MessagePack models here.
- Never store passwords, refresh tokens or private keys in logs or client configuration.
- Treat Gateway responses and transfer tickets as untrusted input and validate them.
- Do not let the client decide authoritative placement, ownership, credits or inventory.
- Preserve the existing game simulation unless a change is explicitly part of the MMO integration.
- Add tests for login state, reconnect, instance display and transfer failure paths.

## Change process

1. Identify whether the change belongs in the client or in Gateway/Protocol/Cluster.
2. Keep the smallest possible patch against the upstream fork.
3. Build the client and run the relevant UI and protocol tests.
4. Document any upstream conflict or compatibility assumption.
