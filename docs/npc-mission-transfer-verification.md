# Mission NPC handoff observation — 2026-10-03

This is the isolated two-instance test: li01 owns New York (`li01`), and the
instance called li02 owns Colorado (`li03`). Gateway `0607554` and Coordinator
`1347938` were running with their dedicated private mission-authority key and
Gateway migration 005 applied. The GameServers included patches 1117 and 1118.

The collected Mission_01a was temporarily replaced using the reusable Scripts
fixture with one transport, one escort, a formation and an active 600-second
trigger. Original story cinematics were excluded. The isolated Test pilot's empty
loadout was restored from the collected new-character loadout; its Gateway lease
was not reassigned by that repair.

## Confirmed handoff

At 19:17:45–19:17:49 local time (Europe/Berlin), transfer
`8e6feed1-3c29-4c45-adbe-f75ab983d284` completed the ownership operation:

- Gateway durable character decision: `6 / Committed`.
- Character 1 lease: instance `li02`, lease version `1` (source version was `0`).
- Coordinator NPC journal: `7 / SourceReleased`, with MissionRuntimeId equal to
  the same transfer ID.
- NPC IDs `01a102c5-5f8f-7158-b051-bd0dbe6826a5` (escort) and
  `01a102c5-5f8f-7cb1-847f-624e8ebfaa11` (transport): owned by `li02` / `li03`,
  ownership version `2`, no active transfer reservation.
- Simulation diagnostics: neither ID remained active on the source; both IDs
  were active on the target with the transport as their formation leader.

The simulation captures are retained in [source before](evidence/mission-transfer/source-before.json),
[source after](evidence/mission-transfer/source-after.json) and
[target after](evidence/mission-transfer/target-after.json).

## Fixes and practical limits

Patch 1117 defers subsequent mission world actions while a mission NPC returned
by DoSpawn still awaits Coordinator identity allocation and world registration.
This resolved the observed null leader crash in Act_SpawnFormation. The simulation
continues while waiting; transfer draining rejects a pending registration.

Patch 1118 gives stateless conditions an explicit empty ConditionStorage. The
active Cnd_Timer previously caused snapshot capture to dereference null. Empty
storage round trips as kind `none` and rejects other storage kinds. Seven focused
condition/mission-commit tests passed, and the LLServer Debug build succeeded.
The complete maintained patch stack through 1118 applied in a fresh checkout.

This run does not prove uninterrupted playable mission transfer: after arriving,
the client crashed with `History 2334 missing id -43`. Patch 1119 corrects a packet
packer error where an oversized first candidate's ID was still emitted although
its update was skipped. Its regression checks the first packet and acknowledged
follow-up deltas. All 59 focused packet/condition tests passed, and the full
patch stack through 1119 applied in a fresh checkout. Live confirmation of this
correction remains necessary; the running test servers still use the 1118 build.

The target capture also shows that the normal player-bound restore retained
source NPC coordinates instead of rebasing at the target gate. That path must use
the arrival translation already applied by recovery/population restoration.
Mission timer continuity after arrival, reconnect recovery, crash-phase coverage
and runtime checkpoint/death retirement remain outstanding acceptance work.
