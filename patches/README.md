# Client patch groups

`series` applies ordered client patches against the commit pinned in `base-client-commit`. Advance that pin when rebasing the patch stack onto a new upstream base. Shared Protocol contracts stay in the Protocol repository and are not duplicated as client patches.
Patches `2000`–`2005` establish the main functional overlays:

1. `2000-gateway-player-transfer.patch` — Gateway login, character transfer,
   mission transfer state, and persistence contracts.
2. `2001-npc-population-lifecycle.patch` — stable NPC identity, simulation
   snapshots, population handoffs, checkpointing, and recovery.
3. `2002-server-operations-permissions.patch` — LLServer operations, admin
   command authorization, and permission handling.
4. `2003-client-ui-render-compatibility.patch` — client screens, interface
   integration, and material/render compatibility.
5. `2004-build-test-integration.patch` — build hooks and test project wiring.
6. `2005-npc-trader-routing.patch` — market-backed NPC trader cargo, system routing,
   value-tiered escorts, and convoy combat/escape behavior.

Patches from `2006` onward carry upstream adoptions, issue fixes, and regression
coverage as separate reviewable changes. Keep them in `series` order; later
patches may depend on earlier ones.
Use `python3 scripts/verify-lancer-nexus-overlay.py` to compare the applied
workspace with a clean reconstruction of the series. CI fetches full history so it can read
the pinned baseline commit. The patch applier records a verified complete overlay on first use; on a baseline checkout it applies the series in order.
