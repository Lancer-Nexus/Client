# Trader transfer verification — 2026-10-03

This evidence is from the isolated first-client E2E environment, not the eight-instance baseline.
LI01 owns `li01` (New York); the instance called LI02 owns `li03` (Colorado).
Both run Client commit `b9320506`, including patches 1111 and 1112.

## Observed handoffs

At 08:44:41 UTC, four autonomous trader groups had completed their LI01 → LI02 handoff:

| Transfer ID | Journal state |
| --- | --- |
| `fab4a46f-b980-432b-81aa-944d364c310f` | `7 / SourceReleased` |
| `cc92f094-2f8e-412c-a960-244e1ca3c6c6` | `7 / SourceReleased` |
| `e9e6a8d7-ae5a-49ca-ad53-222471a45888` | `7 / SourceReleased` |
| `6fee8319-83fe-4dc3-aa7b-e7d5c59e611f` | `7 / SourceReleased` |

The Coordinator MySQL lease registry contained 12 NPCs owned by `li02` / `li03`,
all with ownership version 2. The target's runtime status reported ready with
zero connected players. Target logs showed `spacepop_48_0`, `spacepop_45_0`,
`spacepop_51_0` and `spacepop_35_0` starting their next cruise route toward
`Li03_to_Li01_hole`; the target world remained active beyond its former
two-second playerless shutdown threshold.

For the first group, the durably staged snapshot contained these same stable IDs,
each captured at ownership version 1 and found in the target lease registry at version 2:

- `01a100ea-7e91-7ae2-a6d2-1c2a55929a4e` (`spacepop_48_0`)
- `01a100ea-7e91-7278-ba9d-c19ae4c3d923` (`spacepop_49_1`)
- `01a100ea-7e91-7265-94d4-e390b186dac2` (`spacepop_50_2`)

The snapshot SHA-256 was
`208CC1A3B8BED0605B063573518D6ABC74DD5E3C83262AEB0B9AA52E4106AD8F`.
The target had a corresponding activation receipt. The source snapshot file was released.
Previously aborted journal rows and durable snapshots were not manually deleted or reassigned.

## Supporting checks

- Twelve focused tests passed: NPC AI transfer state, population transfers and autopilot state.
- The entire maintained patch series applied in a fresh detached checkout.
- LLServer Debug build completed with zero errors and two existing warnings.
- The reusable Protocol `tools/NpcTransferDiagnostics` probe repeatedly authenticated
  the target certificate for `li02` and negotiated `lancer-nexus-npc-transfer/1`.

Patch 1111 corrects snapshot validators to preserve real finite formation throttle
and world-space steering values, and re-adopts failed groups into trader traffic.
Patch 1112 retains transferred groups and pending handoff worlds without players,
while leaving new ambient spawning dependent on player presence.

## Remaining acceptance work

These observations prove the first autonomous group handoffs, durable staging,
lease commit and playerless target route activation. They do not yet prove a completed
return jump, continuous position progress of every escort, runtime duplicate counts,
all source/target/Coordinator crash phases, full-instance rejection or coupled mission
NPC handoffs. Those checks remain required for the complete transfer objective.

## Follow-up: return transfer and restart

With Client `7f5ab532`, Protocol `e8bae56`, Coordinator `391163d` and Agent
`8e6c0b8`, the private peer endpoints are advertised independently of the game
endpoint. LI01 uses `quic://127.0.0.2:26455/`, LI02 uses
`quic://127.0.0.3:26456/`. Both mTLS probes succeeded in both directions. Using
the same QUIC port for two local processes had allowed one direction while the
other failed ALPN negotiation; separate ports resolved that observed failure.

After LI02 was restarted, its recovery restored the four original groups from
the committed journals and restarted their routes without a connected player.
They then completed these LI02 → LI01 return handoffs:

| Transfer ID | UTC completion | State |
| --- | --- | --- |
| `4de73552-d63b-495b-afdf-bc2aa0b89d01` | 09:01:12 | `SourceReleased` |
| `a8082c81-6ceb-47c6-9458-8c09d127d6de` | 09:01:29 | `SourceReleased` |
| `31624ac1-0dcc-40af-87cc-4bdafd52d6e6` | 09:01:30 | `SourceReleased` |
| `eba587d6-d19d-4a4f-9148-bec0af37e5b7` | 09:01:52 | `SourceReleased` |

All twelve original IDs were now leased to LI01 at ownership version 3.
The three IDs listed above were individually verified with no active transfer.
Source logs showed their leaders starting the next route toward `Li01_to_Li03`.
Two further outbound groups had also committed by this observation.

Patch 1114 fixes a live crash during ambient formation creation: recovered groups
have no original EncounterInfo. The regression test covers both this null case
and the normal simultaneous-formation exclusion. Fourteen focused Client tests,
49 Protocol tests and 14 Agent tests passed. Coordinator reported 35 passing
tests and one skipped integration test. The fresh full patch stack applied and
the LLServer build finished with zero errors and 179 existing warnings.

The return jump and committed-target restart are now observed. Runtime duplicate
counts, all crash phases and coupled mission transfers remain unverified.
An additional audit found that Coordinator's recovery ownership check currently
matches the instance but not the snapshot's ownership version. A later complete
round trip to the same instance could therefore make an older journal eligible
again. This needs a version-fencing correction and regression test before full
recovery acceptance.

## Follow-up: exact recovery fences and active simulation

Coordinator `621f54b` now checks the exact expected lease versions for direct
recovery and discovery pages. Target recovery requires the captured source
version plus one; durable aborted-source recovery requires the captured version.
The MySQL integration test performs multiple real round trips and rejects both
old committed journals and old aborted rollback snapshots after returning to the
same instance. All 36 Coordinator tests passed with the isolated MySQL server;
none were skipped. On the live environment, the first group's leases were back
on LI02 at version 6, while the original journal's recovery request returned 404.
Current eligible journals still returned 200. No schema migration was required.

Client `b626519d` adds stable-ID freeze lookup, namespaced population nicknames
and local Debug-only `npc-state` diagnostics. Seventeen focused Client tests and
five Scripts comparison tests passed. A fresh complete patch stack applied and
the LLServer Debug build completed with zero errors and two existing warnings.
Both servers were restarted with their journals and staging data preserved.

The committed [source sample](evidence/npc-state-li01.json) and
[target sample](evidence/npc-state-li02-before.json) contained 21 distinct active
NPC IDs with no intersections. The original three IDs listed above appeared
exactly once each, only on LI02 at version 10, matching their live MySQL leases.
The lease versions remained unchanged across sampling. The
[second target sample](evidence/npc-state-li02-after.json) verified movement:

| NPC ID suffix | Distance moved |
| --- | --- |
| `1c2a55929a4e` (leader) | 19038.20 |
| `c19ae4c3d923` (escort) | 19104.14 |
| `e390b186dac2` (escort) | 18380.09 |

Reproduce the selected-ID checks from the workspace root:

```bash
python3 Scripts/tools/compare-npc-state.py Client/docs/evidence/npc-state-li01.json Client/docs/evidence/npc-state-li02-before.json --npc 01a100ea-7e91-7ae2-a6d2-1c2a55929a4e
python3 Scripts/tools/compare-npc-state.py Client/docs/evidence/npc-state-li02-before.json Client/docs/evidence/npc-state-li02-after.json --movement --npc 01a100ea-7e91-7ae2-a6d2-1c2a55929a4e
```

These are observations from separate simulation queues, not a global atomic
snapshot. They provide active-copy and movement evidence for the tested interval.
All crash phases and coupled mission handoffs still need acceptance testing.
Another recovery boundary remains open: deaths and later runtime changes after a
completed transfer are not checkpointed by the transfer journal. Recovery from
that journal alone can replay state preceding such events; this needs explicit
checkpoint or retirement handling before claiming exact recovery of ongoing NPCs.
