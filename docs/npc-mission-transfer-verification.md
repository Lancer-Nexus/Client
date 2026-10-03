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

## Mission NPC destruction after restoration (patch 1124)

Inspection found that `SpawnShipInWorld` installed the mission destruction callback
only after `NPCManager.DoSpawn` returned. `RestoreTransfer` calls `DoSpawn` directly,
so imported NPCs lacked that callback. Their destruction could leave mission labels
alive and prevent `Cnd_Destroyed` from completing.

Patch 1124 binds the callback in `SDestroyableComponent` construction when `DoSpawn`
receives a MissionRuntime. This covers fresh spawns, local jumpers and transferred
NPCs. The callback captures the owning runtime and nickname; ambient NPCs have no
mission callback. The redundant assignment in the mission spawn action is removed.

Three regression tests exercise the actual component `Destroy` path with a
restored MissionRuntime, label and `Cnd_Destroyed` state; a second runtime sharing
the nickname remains unaffected. Population NPCs retain no mission callback.
Twelve focused tests and the broader 73-test NPC/mission/entry regression selection
passed. LLServer built with zero errors and two warnings. The complete maintained
patch series was applied to a fresh checkout for validation.

These are automated component and runtime checks. Destruction of an imported NPC
in a live game has not yet been exercised. The running test processes still use
build 1123; patch 1124 takes effect on their next rebuild/deployment. Durable NPC
retirement/checkpoints and mission reconnect recovery remain open.

## Mission actor ownership at transfer boundaries (patch 1125)

`GatherJumpers` previously selected ships by their global world nickname. Two
players running the same mission can share scripted names, so that lookup could
capture another player's NPC. The initial snapshot check matched only the mission
nickname and did not compare the mission character ID.

Patch 1125 resolves a jumper from the active objects of its exact MissionRuntime,
with case-insensitive script nickname matching. Ambiguous duplicate actors within
one runtime are rejected. Both source freeze paths recheck the live NPC's runtime
before capture/removal. Snapshot construction requires the same character ID and
mission nickname. Target restoration validates every NPC's context before creating
any member, and refuses a mission group without its owning runtime.

Eleven added cases cover competing runtimes with identical NPC names, inactive
actors, duplicate actors, the source freeze guard, matching/mismatched character and
mission context, and target rejection before any object is created. All 84 selected
NPC/mission/entry regression tests passed. The maintained patch stack applied to a
fresh checkout; the final test fixture correction was checked/applied there too.
The full Solution build built the client and LLServer, but still failed on the two
previously documented missing administration types in LLServerGui.

This is transfer-boundary ownership protection. General mission actions that look
up actors by world nickname still require an audit for concurrent missions; this
patch does not establish complete mission actor namespacing. Live deployment and a
multi-player mission transfer test remain pending, along with checkpoints,
retirement, reconnect recovery and the remaining crash-phase acceptance cases.

## Durable retirement outbox and recovery barrier (patch 1126)

The Client Protocol submodule now uses `b97964e`, including the retirement contracts
and the appended `NpcOwnershipLease.IsRetired` field. NPC ID allocation rejects
retired leases. Engine changes remain maintained in patch 1126.

`GameServer.QueueNpcRetirement` queues a bounded request without doing file or HTTP
I/O on the calling simulation thread. Separate persistence and delivery workers
write MessagePack intent with a flushed temporary file and atomic rename before
sending the request. Each batch contains at most 256 NPCs; the intake queue holds
at most 1024 batches and reports overload rather than dropping intent. HTTP uses
HTTPS (or loopback development HTTP), no redirects, a four-second attempt timeout
and bounded retry backoff. Unknown delivery outcomes keep the request ID and bytes.

The API exposes separate durability and Coordinator-response tasks. A successful
batch response still requires checking each result. Confirmed responses remain on
disk and block replay at their original NPC ownership fence. Pending requests also
block activation. Startup loads these records before readiness; malformed records
fail closed. The server's restore path checks this barrier before creating any
member. An index avoids scanning all queued records for each restored NPC.

Tests cover persistence behind a blocked HTTP request, exact request replay after
outbox recreation, rejection of mismatched replies, confirmed fences after another
restart, corruption blocking readiness, and pending intent blocking the actual
`RestoreTransfer` path before object creation. This establishes local process
restart behavior; it does not claim whole-machine power-loss durability or shared
storage coordination between multiple processes using the same staging directory.

The terminal event hooks are not yet connected: `SDestroyableComponent.Destroy`
and `SNPCComponent.Docked` currently keep their existing behavior. They must be
coupled to survivor/MissionRuntime checkpoints before retiring a member of an old
formation journal, since that journal no longer authorizes recovery of the whole
group once one member's fence changes. Therefore this patch does not yet prove
crash-safe death/docking or complete mission reconnect recovery.

Final verification: all 90 selected NPC/mission/entry regression tests passed,
including cancellation of a waiting caller during retry backoff. LLServer built
with zero errors. The complete series applied in a fresh checkout; the final
maintained patch was then reversed, checked and applied there after refinements.
The full Solution build still reports the two existing missing LLServerGui admin
types while building the client and LLServer. Running test processes remain on
build 1123; no new runtime deployment or whole-process crash test was performed
for this infrastructure change.

## Corrected build diagnosis and overlay integrity

The previously reported LLServerGui failures were caused by local overlay drift,
not by a missing fix in the maintained series. Patch 1098 already removes the
obsolete GUI admin list/actions and event payload references. It was applied in
the fresh validators but absent from the main working file. The local `build.sh`
also lacked the error-propagation change already maintained in patch 1097.
Applying these existing patches restored the intended build inputs.

A clean reconstruction now matches all 113 files touched by the current series,
allowing only CRLF/LF differences. The complete `LibreLancer.sln` build succeeded
with zero errors and 317 warnings, including LLServerGui. Earlier failed-build
entries above describe their actual historical outputs; their attribution to a
separate preexisting GUI defect is superseded by this diagnosis.

The reusable `scripts/verify-lancer-nexus-overlay.py` reconstructs HEAD plus the
series in temporary directories and compares it with the applied source. It is
now mandatory before `--record-current` writes fingerprints, preventing an
incomplete overlay from being marked verified. Seven tests cover complete and
reverted overlays, later patches changing earlier output, CRLF compatibility,
unreconstructable patches and the record command's acceptance/rejection behavior.
This build repair does not complete the pending checkpoint, terminal-event,
reconnect or crash-phase acceptance work; running services were not redeployed.
