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
