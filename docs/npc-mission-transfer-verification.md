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

## Follow-up: playable round trip with exact timer continuation

Both GameServers and the Debug client now run the overlay through patch 1123.
The independent fixture pilot `MissionConvoy` (character 2) completed these live
handoffs without a client crash:

| Direction | Transfer ID | NPC journal | Character lease | NPC ownership |
| --- | --- | --- | --- | --- |
| li01 → li02 / Li03 | `185df954-8084-4f0c-a005-eb8a7b7a3b71` | SourceReleased | li02, version 1 | li02, version 2 |
| li02 → li01 / Li01 | `f5be32d8-8c6a-4b16-9c24-37f730f5349a` | SourceReleased | li01, version 2 | li01, version 3 |

Gateway has permanent `Committed` decisions for both transfer IDs. NPC IDs
`01a102e0-c2d4-717c-aab6-58ead1826d10` (escort) and
`01a102e0-c2d4-72ba-bd43-afcf53fae708` (transport) remained stable in both directions.
Simulation captures prove exactly one active copy of each ID after each handoff,
with the transport still the formation leader. They arrived at the destination
gate instead of retaining source-system coordinates. The client remained connected
on both destinations, and the convoy was visible in the target HUD.

The frozen journal snapshots provide a stronger timer check than merely seeing an
increasing counter. For the idle 600-second fixture trigger:

| Capture | Frozen seconds | Target SpawnPlayer tick | Capture tick | Actual seconds | Difference from frozen + elapsed ticks |
| --- | ---: | ---: | ---: | ---: | ---: |
| Colorado after outbound | 115.5668978 | 8294 | 12970 | 193.5003870 | +0.0001559 s |
| New York after return | 329.7506595 | 21288 | 31431 | 498.8009976 | +0.0003381 s |

Both NPCs report the same continued MissionRuntime state. Random state, labels,
condition storage and formation leader were preserved. The reusable Scripts
`check-npc-mission-continuity.py` verifier checks this against the JSON export from
Protocol's `NpcTransferDiagnostics`, including the expected ownership fence.

Evidence is retained under [mission-transfer-1123](evidence/mission-transfer-1123/source-before.json):
[source after outbound](evidence/mission-transfer-1123/source-after.json),
[target after outbound](evidence/mission-transfer-1123/target-after.json),
[source after return](evidence/mission-transfer-1123/source-return.json),
[target after return](evidence/mission-transfer-1123/target-return.json),
[outbound continuity](evidence/mission-transfer-1123/outbound-continuity.json) and
[return continuity](evidence/mission-transfer-1123/return-continuity.json). Frozen
snapshot exports are stored alongside these captures.

Patch 1120 routes normal mission login through the same arrival translation as
recovery. Patch 1121 adds MissionRuntime state to local Debug NPC diagnostics.
Patch 1122 handles empty visit lists in both encoder and decoder. Patch 1123
hydrates character location before initial RPCs so a failed entry cannot persist
an uninitialized location on disconnect. The empty-visit failure had exposed this
second issue in the independent fixture; its local position was repaired with the
source stopped, while preserving cargo and the authoritative Gateway lease.

Twenty-two focused Client tests passed. LLServer built with zero errors. The full
Solution build also built the client, but failed in the existing LLServerGui admin
panel due to unresolved `CharacterAdminChangedEventPayload` and
`AdminCharacterDescription` types. That separate GUI build defect remains open.
The complete patch series through 1122 applied in a fresh checkout, followed by
successful application of patch 1123 to that same checkout.

This proves the live idle mission fixture round trip, arrival translation and
exact timer continuation. It does not prove all mission conditions, mission
reconnect/process-failure recovery, runtime checkpoints/death retirement or every
capacity/mTLS/crash-phase failure scenario. Those remain acceptance work.
