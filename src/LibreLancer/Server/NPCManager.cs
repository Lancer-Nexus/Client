using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LibreLancer.Data;
using LibreLancer.Data.GameData;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.Schema.Pilots;
using LibreLancer.Data.Schema.Solar;
using LibreLancer.Missions;
using LibreLancer.Net.Protocol;
using LibreLancer.Server.Components;
using LibreLancer.World;
using LibreLancer.World.Components;
using LancerNexus.Protocol;
using MessagePack;
using LibreLancer.Server.Ai;
using Pilot = LibreLancer.Data.GameData.Pilot;

namespace LibreLancer.Server
{
    public class NPCManager
    {
        private NPCWattleScripting scripting;
        public ServerWorld World;
        private Random rand = new();
        public NPCManager(ServerWorld world)
        {
            this.World = world;
            scripting = new NPCWattleScripting(this, world.Server.GameData);
        }

        public Task<string> RunScript(string src)
        {
            TaskCompletionSource<string> source = new TaskCompletionSource<string>();
            World.EnqueueAction(() =>
            {
                source.SetResult(scripting.Run(src));
            });
            return source.Task;
        }

        public int AttackingPlayer = 0;
        public bool HostileClamp = false; // NPCs wont clamp unless explicitly enabled
        public int PlayerEnemyClampMin = 0;
        public int PlayerEnemyClampMax = 3; //default values

        public void SetHostileClamp(bool enabled)
        {
            HostileClamp = enabled;
        }

        public void SetPlayerEnemyClamp(int min, int max)
        {
            HostileClamp = true;
            PlayerEnemyClampMin = Math.Max(0, min);
            PlayerEnemyClampMax = Math.Max(PlayerEnemyClampMin, max);
        }

        public void FrameStart()
        {
            AttackingPlayer = 0;
        }

        public void Despawn(GameObject obj, bool exploded)
        {
            World.RemoveSpawnedObject(obj, exploded);
        }

        private Dictionary<string, GameObject> missionNPCs = new(StringComparer.OrdinalIgnoreCase);

        // DoSpawn can return an inert object while the Coordinator supplies its identity.
        // Subsequent mission actions must wait for registration on the simulation queue.
        public bool HasPendingMissionSpawns(MissionRuntime? runtime) => runtime != null &&
            missionNPCs.Values.Any(obj => obj.NetID == 0 &&
                obj.TryGetComponent<SNPCComponent>(out var npc) && npc.MissionRuntime == runtime);

        private sealed record ResolvedNpcFormation(NpcFormationStateV1 State,
            (NpcFormationMemberV1 Member, GameObject Object)[] Members);

        public void NpcDoAction(string nickname, Action<GameObject> act)
        {
            World.EnqueueAction(() =>
            {
                if (!missionNPCs.TryGetValue(nickname, out var npc))
                {
                    FLLog.Error("Mission", $"Could not find spawned npc {npc}");
                    return;
                }
                act(missionNPCs[nickname]);
            });
        }

        // Should be replaced with Faction class creating a random def
        public ObjectName RandomName(Faction? fac)
        {
            if (fac == null)
            {
                return new ObjectName("NULL");
            }

            var rand = new Random();
            ValueRange<int>? firstName = null;
            if (fac.Properties!.FirstNameMale != null &&
                fac.Properties.FirstNameFemale != null)
            {
                firstName = rand.Next(0, 2) == 1 ? fac.Properties.FirstNameMale : fac.Properties.FirstNameFemale;
            }
            else if (fac.Properties.FirstNameFemale != null)
            {
                firstName = fac.Properties.FirstNameFemale;
            }
            else if (fac.Properties.FirstNameMale != null)
            {
                firstName = fac.Properties.FirstNameMale;
            }
            return new ObjectName(firstName != null ? rand.Next(firstName.Value) : 0, rand.Next(fac.Properties.LastName));
        }

        public GameObject SpawnJumper(JumperNpc jumper, MissionRuntime msn, string jumpObject)
        {
            var jumpPoint = World.GameWorld.GetObject(jumpObject);
            var pos = jumpPoint!.WorldTransform.Position;
            var orient = jumpPoint.WorldTransform.Orientation;
            pos = Vector3.Transform(new Vector3(rand.Next(-50, 50), rand.Next(-50, 50), rand.Next(-300, -100)),
                orient) + pos;
            var newObj = DoSpawn(
                jumper.Name,
                jumper.Nickname,
                jumper.Faction,
                jumper.StateGraph?.Description?.Name,
                jumper.SpaceCostume,
                jumper.Loadout,
                jumper.Pilot,
                pos,
                orient,
                null, 0,
                msn,
                stableNpcId: jumper.NpcId,
                stableNpcOwnershipVersion: jumper.OwnershipVersion);
            msn.SystemEnter(World.System.Nickname, jumper.Nickname);
            return newObj;
        }

        public GameObject DoSpawn(
            ObjectName name,
            string? nickname,
            Faction? affiliation,
            string? stateGraph,
            CostumeEntry? costume,
            ObjectLoadout loadout,
            Pilot? pilot,
            Vector3 position,
            Quaternion orient,
            string? arrivalObj,
            int arrivalIndex,
            MissionRuntime? msn = null,
            bool registerNpc = true,
            bool arrivalIndexReserved = false,
            IEnumerable<GameObject>? neutralTo = null,
            Guid? stableNpcId = null,
            long stableNpcOwnershipVersion = 0,
            bool deferActivation = false
            )
        {
            var identityAllocator = stableNpcId.HasValue ? null : World.Server.GetNpcIdentityAllocator(World.System.Nickname);
            NpcOwnershipLease? lease = null;
            var npcId = stableNpcId ?? Guid.NewGuid();
            if (!stableNpcId.HasValue && identityAllocator != null &&
                identityAllocator.TryTakeLease(out var assignedLease))
            {
                lease = assignedLease;
                npcId = assignedLease.NpcId;
            }
            else if (!stableNpcId.HasValue && identityAllocator != null)
            {
                npcId = Guid.Empty;
            }
            var ship = World.Server.GameData.Items.Ships.Get(loadout.Archetype);
            GameObject spawnPoint = World.GameWorld.GetObject(arrivalObj)!;
            SDockableComponent? sdock = null;
            if (spawnPoint?.TryGetComponent<SDockableComponent>(out sdock) ?? false)
            {
                var reservedHere = arrivalIndexReserved;
                if (!reservedHere)
                {
                    var reserved = arrivalIndex == 0
                        ? sdock.TryReserveUndockIndex(out arrivalIndex)
                        : sdock.TryReserveUndockIndex(arrivalIndex);
                    if (!reserved)
                    {
                        FLLog.Warning("NPC", $"Could not reserve spawn point for {arrivalObj}");
                        sdock = null;
                    }
                    else
                    {
                        reservedHere = true;
                    }
                }

                if (sdock != null && sdock.TryGetSpawnPoint(arrivalIndex, out var p))
                {
                    position = p.Position;
                    orient = p.Orientation;
                }
                else if (sdock != null)
                {
                    if (reservedHere)
                        sdock.ReleaseUndockIndex(arrivalIndex);
                    FLLog.Warning("NPC", $"Could not get spawn point {arrivalIndex} for {arrivalObj}");
                    sdock = null;
                }
            }
            var obj = new GameObject(ship!, World.Server.Resources, false, true)
            {
                Name = name,
                Nickname = nickname
            };
            obj.SetLocalTransform(new Transform3D(position, orient));
            obj.AddComponent(new SHealthComponent(obj)
            {
                CurrentHealth = ship!.Hitpoints,
                MaxHealth = ship.Hitpoints
            });
            obj.AddComponent(new SFuseRunnerComponent(obj) { DamageFuses = ship.Fuses });
            foreach (var equipped in loadout.Items)
            {
                EquipmentObjectManager.InstantiateEquipment(obj, World.Server.Resources, null, EquipmentType.Server,
                    equipped.Hardpoint, equipped.Equipment);
            }
            var cargo = new SNPCCargoComponent(obj);
            cargo.Cargo.AddRange(loadout.Cargo);
            obj.AddComponent(cargo);
            var stateDescription = new StateGraphDescription(stateGraph!.ToUpperInvariant(), "LEADER");
            World.Server.GameData.Items.Ini.StateGraphDb.Tables.TryGetValue(stateDescription, out var stateTable);
            var npcComponent = new SNPCComponent(obj, this, stateTable!)
            {
                MissionRuntime = msn,
                Faction = affiliation,
                NpcId = npcId,
                OwnershipVersion = stableNpcOwnershipVersion > 0 ? stableNpcOwnershipVersion : lease?.OwnershipVersion ?? 0
            };
            npcComponent.SetPilot(pilot);
            npcComponent.CommHead = costume?.Head;
            npcComponent.CommBody = costume?.Body;
            npcComponent.CommHelmet = costume?.Accessory;
            obj.AddComponent(new SelectedTargetComponent(obj));
            obj.AddComponent(npcComponent);
            obj.AddComponent(new AutopilotComponent(obj));
            obj.AddComponent(new ShipSteeringComponent(obj));
            obj.AddComponent(new ShipPhysicsComponent(obj, ship));
            obj.AddComponent(new WeaponControlComponent(obj));
            SAutoTurretComponent.TryAdd(obj, () => npcComponent.Pilot?.Gun);
            obj.AddComponent(new SDestroyableComponent(obj, World, msn));            obj.AddComponent(new DirectiveRunnerComponent(obj));
            if (neutralTo != null && obj.TryGetComponent<SRepComponent>(out var rep))
            {
                foreach (var neutralTarget in neutralTo)
                    rep.SetAttitude(neutralTarget, RepAttitude.Neutral);
            }
            // NPCs spawn already cloaked
            if (obj.TryGetComponent<CloakComponent>(out var cloak))
            {
                cloak.SetInitCloaked();
            }
            void ActivateNpc()
            {
                World.OnNPCSpawn(obj);
                if (sdock != null)
                {
                    sdock.UndockShip(obj, World.GameWorld, arrivalIndex);
                    obj.GetComponent<AutopilotComponent>()!.Undock(spawnPoint!, arrivalIndex);
                }
            }
            if (deferActivation)
            {
                // The transfer restore path configures all runtime state before adding this object to the simulation.
            }
            else if (npcId == Guid.Empty)
            {
                identityAllocator!.WhenLeaseAvailable(assignedLease => World.EnqueueAction(() =>
                {
                    npcComponent.NpcId = assignedLease.NpcId;
                    npcComponent.OwnershipVersion = assignedLease.OwnershipVersion;
                    ActivateNpc();
                }));
            }
            else
            {
                ActivateNpc();
            }
            if (registerNpc && nickname != null)
            {
                missionNPCs[nickname] = obj;
            }
            return obj;
        }

        /// <summary>Recreates an inert NPC transfer snapshot after Coordinator ownership commit.</summary>
        public GameObject[] RestoreTransfer(NpcTransferSnapshot snapshot, MissionRuntime? missionRuntime = null,
            bool incrementOwnershipVersion = true, string? expectedSystemId = null,
            bool emitMissionSystemTransition = true)
        {
            NpcTransferContractValidator.Validate(snapshot);
            expectedSystemId ??= snapshot.TargetSystemId;
            if (!string.Equals(expectedSystemId, World.System.Nickname, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("NPC transfer snapshot targets a different world.");
            if (snapshot.MissionRuntimeId.HasValue)
            {
                if (missionRuntime is null)
                    throw new InvalidDataException("Mission NPC restoration requires its owning MissionRuntime.");
                // Validate the whole group before creating or activating any member.
                foreach (var identity in snapshot.Npcs)
                    GameServer.ValidateMissionNpcContext(missionRuntime,
                        MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(identity.RuntimeState));
            }
            foreach (var identity in snapshot.Npcs)
            {
                var targetVersion = incrementOwnershipVersion ? checked(identity.OwnershipVersion + 1) : identity.OwnershipVersion;
                if (World.Server.BlocksNpcRetirementActivation(identity.NpcId, targetVersion))
                    throw new InvalidOperationException("NPC snapshot contains pending or confirmed retirement intent.");
            }
            var existingNpcIds = World.GameWorld.Objects
                .Select(obj => obj.TryGetComponent<SNPCComponent>(out var npc) ? npc.NpcId : Guid.Empty)
                .Where(id => id != Guid.Empty).ToHashSet();
            var missingEntries = snapshot.Npcs.Where(npc => !existingNpcIds.Contains(npc.NpcId)).ToArray();
            if (missingEntries.Length == 0)
            {
                RestoreFormations(ResolveFormations(snapshot.Formations, []));
                return [];
            }
            if (missingEntries.Length != snapshot.Npcs.Length)
            {
                snapshot = new NpcTransferSnapshot
                {
                    TransferId = snapshot.TransferId,
                    FormationId = snapshot.FormationId,
                    MissionRuntimeId = snapshot.MissionRuntimeId,
                    NpcIds = missingEntries.Select(npc => npc.NpcId).ToArray(),
                    Npcs = missingEntries,
                    TargetSystemId = snapshot.TargetSystemId,
                    TargetArrivalObject = snapshot.TargetArrivalObject,
                    MissionRuntimeState = snapshot.MissionRuntimeState,
                    Formations = snapshot.Formations
                };
            }
            var restored = new List<(NpcRuntimeSnapshot Identity, NpcRuntimeStateV1 State, GameObject Object)>();
            var autopilotStates = new Dictionary<GameObject, AutopilotTransferState>();
            foreach (var identity in snapshot.Npcs)
            {
                if (identity.OwnershipVersion == long.MaxValue)
                    throw new InvalidDataException("NPC ownership version cannot be incremented safely.");
                var state = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(identity.RuntimeState);
                if (string.IsNullOrWhiteSpace(state.Nickname) || string.IsNullOrWhiteSpace(state.DisplayName) ||
                    string.IsNullOrWhiteSpace(state.StateGraphId))
                    throw new InvalidDataException("NPC runtime snapshot is missing the identity required for restoration.");

                var ship = World.Server.GameData.Items.Ships.Get(state.LoadoutArchetype)
                    ?? throw new InvalidDataException($"NPC ship archetype '{state.LoadoutArchetype}' is unavailable.");
                var loadout = new ObjectLoadout { Archetype = ship.Nickname };
                foreach (var item in state.Equipment)
                {
                    var equipment = World.Server.GameData.Items.Equipment.Get(item.EquipmentId)
                        ?? throw new InvalidDataException($"NPC equipment '{item.EquipmentId}' is unavailable.");
                    loadout.Items.Add(new LoadoutItem(item.Hardpoint, equipment));
                }
                foreach (var cargo in state.Cargo)
                {
                    var equipment = World.Server.GameData.Items.Equipment.Get(cargo.ItemId)
                        ?? throw new InvalidDataException($"NPC cargo item '{cargo.ItemId}' is unavailable.");
                    loadout.Cargo.Add(new BasicCargo(equipment, cargo.Count, cargo.Hardpoint));
                }
                var faction = string.IsNullOrWhiteSpace(state.FactionId)
                    ? null
                    : World.Server.GameData.Items.Factions.Get(state.FactionId)
                      ?? throw new InvalidDataException($"NPC faction '{state.FactionId}' is unavailable.");
                var pilot = string.IsNullOrWhiteSpace(state.PilotId)
                    ? null
                    : World.Server.GameData.Items.GetPilot(state.PilotId)
                      ?? throw new InvalidDataException($"NPC pilot '{state.PilotId}' is unavailable.");
                var costume = new CostumeEntry
                {
                    Head = string.IsNullOrWhiteSpace(state.CommHeadId) ? null : World.Server.GameData.Items.Bodyparts.Get(state.CommHeadId),
                    Body = string.IsNullOrWhiteSpace(state.CommBodyId) ? null : World.Server.GameData.Items.Bodyparts.Get(state.CommBodyId),
                    Accessory = string.IsNullOrWhiteSpace(state.CommAccessoryId) ? null : World.Server.GameData.Items.Accessories.Get(state.CommAccessoryId)
                };
                var stateDescription = new StateGraphDescription(state.StateGraphId.ToUpperInvariant(), "LEADER");
                if (!World.Server.GameData.Items.Ini.StateGraphDb.Tables.TryGetValue(stateDescription, out var graph))
                    throw new InvalidDataException($"NPC state graph '{state.StateGraphId}' is unavailable.");
                var obj = DoSpawn(new ObjectName(state.DisplayName), state.Nickname, faction, state.StateGraphId,
                    costume, loadout, pilot, ToVector(state.Position), ToQuaternion(state.Orientation), null, 0,
                    missionRuntime, registerNpc: false, stableNpcId: identity.NpcId,
                    stableNpcOwnershipVersion: incrementOwnershipVersion
                        ? identity.OwnershipVersion + 1
                        : identity.OwnershipVersion,
                    deferActivation: true);
                RestoreStructuralParts(obj, state.StructuralParts);
                var npc = obj.GetComponent<SNPCComponent>()!;
                npc.TransferRandomState = state.Ai.RandomState;
                if (obj.TryGetComponent<SHealthComponent>(out var health))
                    health.CurrentHealth = Math.Clamp(state.Health, 0, health.MaxHealth);
                var equipmentComponents = obj.GetComponents<EquipmentComponent>()
                    .Concat(obj.GetChildComponents<EquipmentComponent>()).ToList();
                foreach (var equipmentState in state.Equipment)
                {
                    var equipment = equipmentComponents.FirstOrDefault(candidate =>
                            string.Equals(candidate.Equipment.Nickname, equipmentState.EquipmentId, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(candidate.Parent.Attachment?.Name ?? "internal", equipmentState.Hardpoint,
                                StringComparison.OrdinalIgnoreCase));
                    if (equipment is null)
                        throw new InvalidDataException($"NPC equipment '{equipmentState.EquipmentId}' at '{equipmentState.Hardpoint}' could not be restored.");
                    equipmentComponents.Remove(equipment);
                    if (equipment.Parent.Attachment != null && equipment.Parent.TryGetComponent<SHealthComponent>(out var equipmentHealth))
                    {
                        equipmentHealth.CurrentHealth = Math.Clamp(equipmentState.Health, 0, 1) * equipmentHealth.MaxHealth;
                        if (equipment.Parent.Attachment is { } hardpoint && equipmentState.Health < 1)
                            health?.EquipmentHealths.TryAdd(hardpoint, Math.Clamp(equipmentState.Health, 0, 1));
                    }
                    if (equipment.Equipment is LibreLancer.Data.GameData.Items.PowerEquipment powerEquipment &&
                        equipment.Parent.TryGetComponent<PowerCoreComponent>(out var powerCore))
                        powerCore.CurrentEnergy = Math.Clamp(equipmentState.Energy, 0, 1) * powerEquipment.Def.Capacity;
                    if (equipmentState.ExtensionData.Length > 0)
                    {
                        var extension = JsonSerializer.Deserialize<NpcEquipmentTransferExtensionV1>(equipmentState.ExtensionData)
                            ?? throw new InvalidDataException("NPC equipment transfer extension is empty.");
                        if (extension.SchemaVersion != 1)
                            throw new InvalidDataException("NPC equipment transfer extension version is unsupported.");
                        if (extension.Shield is { } shieldState)
                        {
                            if (!float.IsFinite(shieldState.Health) || shieldState.Health < 0 ||
                                !double.IsFinite(shieldState.SuppressionRemainingSeconds) || shieldState.SuppressionRemainingSeconds < 0 ||
                                !float.IsFinite(shieldState.SuppressedRestoreHealth) || shieldState.SuppressedRestoreHealth < 0 ||
                                !equipment.Parent.TryGetComponent<SShieldComponent>(out var shield))
                                throw new InvalidDataException("NPC shield transfer state is invalid or the target shield is missing.");
                            shield.RestoreTransferState(shieldState, World.GameWorld);
                        }
                        if (extension.WeaponCooldownSeconds is { } weaponCooldown)
                        {
                            if (!double.IsFinite(weaponCooldown) || weaponCooldown < 0 || weaponCooldown > 3600 ||
                                !equipment.Parent.TryGetComponent<WeaponComponent>(out var weapon))
                                throw new InvalidDataException("NPC weapon cooldown transfer state is invalid or the target weapon is missing.");
                            weapon.CurrentCooldown = weaponCooldown;
                        }
                        if (extension.WeaponAngles is { } weaponAngles)
                        {
                            weaponAngles.Validate();
                            if (!equipment.Parent.TryGetComponent<WeaponComponent>(out var weapon))
                                throw new InvalidDataException("NPC weapon angle transfer state has no target weapon.");
                            weapon.RestoreAngles(new Vector2(weaponAngles.Yaw, weaponAngles.Pitch));
                        }
                        if (extension.ThrustCapacityFraction is { } thrustCapacityFraction)
                        {
                            if (!float.IsFinite(thrustCapacityFraction) || thrustCapacityFraction is < 0 or > 1 ||
                                !equipment.Parent.TryGetComponent<PowerCoreComponent>(out var restoredPowerCore))
                                throw new InvalidDataException("NPC thrust capacity transfer state is invalid or the target power core is missing.");
                            restoredPowerCore.CurrentThrustCapacity = thrustCapacityFraction * restoredPowerCore.Equip.ThrustCapacity;
                        }
                    }
                }
                if (state.Autopilot.ExtensionData.Length > 0 && obj.TryGetComponent<AutopilotComponent>(out var autopilot))
                {
                    var autopilotState = JsonSerializer.Deserialize<AutopilotTransferState>(state.Autopilot.ExtensionData)
                        ?? throw new InvalidDataException("NPC autopilot state is empty.");
                    autopilotState.Validate();
                    if (!string.Equals(state.Autopilot.Behavior, autopilotState.Behavior.ToString(), StringComparison.Ordinal))
                        throw new InvalidDataException("NPC autopilot state does not match its declared behavior.");
                    autopilotStates.Add(obj, autopilotState);
                }
                restored.Add((identity, state, obj));
            }

            var aiStates = new Dictionary<GameObject, NpcAiTransferExtensionV1>();
            foreach (var entry in restored)
            {
                if (entry.State.Ai.ExtensionData.Length == 0)
                    throw new InvalidDataException("NPC AI transfer extension is missing.");
                var aiState = JsonSerializer.Deserialize<NpcAiTransferExtensionV1>(entry.State.Ai.ExtensionData)
                    ?? throw new InvalidDataException("NPC AI transfer extension is empty.");
                if (aiState.SchemaVersion != 3)
                    throw new InvalidDataException("NPC AI transfer extension version is unsupported.");
                aiStates.Add(entry.Object, aiState);
                if (entry.State.Ai.StateId is not ("none" or nameof(AiAttackState) or nameof(AiDockState)))
                {
                    throw new InvalidDataException($"NPC AI state '{entry.State.Ai.StateId}' is not supported by this restore version.");
                }
                if (entry.State.Ai.StateId is nameof(AiAttackState) or nameof(AiDockState))
                {
                    if (aiState.DirectiveTarget is null || aiState.GotoKind is null && entry.State.Ai.StateId == nameof(AiDockState))
                        throw new InvalidDataException("NPC directive target or dock behavior is missing.");
                    if (entry.State.Ai.StateId == nameof(AiAttackState) &&
                        aiState.DirectiveTarget.Kind == "npc" && aiState.DirectiveTarget.NpcId != entry.State.CurrentTargetNpcId)
                        throw new InvalidDataException("NPC attack target reference does not match the stable NPC target ID.");
                    if (ResolveTransferReference(aiState.DirectiveTarget, restored) is null)
                        throw new InvalidDataException("NPC directive target could not be resolved after restore.");
                }
            }
            var resolvedFormations = ResolveFormations(snapshot.Formations, restored);
            var previousFormations = resolvedFormations.SelectMany(formation => formation.Members)
                .GroupBy(member => member.Object)
                .ToDictionary(group => group.Key, group => group.Key.Formation);
            RestoreFormations(resolvedFormations);
            foreach (var entry in restored)
            {
                var component = entry.Object.GetComponent<SNPCComponent>()!;
                var ai = entry.State.Ai;
                var extension = aiStates[entry.Object];
                AiState? directive = null;
                if (ai.StateId is nameof(AiAttackState) or nameof(AiDockState))
                {
                    var target = ResolveTransferReference(extension.DirectiveTarget!, restored)!;
                    directive = ai.StateId == nameof(AiAttackState)
                        ? new AiAttackState(target)
                        : new AiDockState(target, (GotoKind)extension.GotoKind!.Value);
                }
                component.SetState(directive, World.GameWorld);
                component.RestoreTransferTimers(ai.PreviousStateId, ai.StateElapsedSeconds,
                    ai.Timers.FirstOrDefault());
                component.RestoreTransferState(extension,
                    reference => ResolveTransferReference(reference, restored));
            }
            foreach (var entry in restored)
            {
                if (autopilotStates.TryGetValue(entry.Object, out var autopilotState) &&
                    entry.Object.TryGetComponent<AutopilotComponent>(out var autopilot))
                    autopilot.RestoreTransferState(autopilotState,
                        reference => ResolveTransferReference(reference, restored));
            }
            var activated = new List<GameObject>(restored.Count);
            try
            {
                foreach (var entry in restored)
                {
                    activated.Add(entry.Object);
                    World.OnNPCSpawn(entry.Object);
                    if (entry.Object.PhysicsComponent is not { Body: { } body })
                        throw new InvalidDataException($"NPC '{entry.State.Nickname}' has no registered physics body after activation.");
                    body.SetTransform(new Transform3D(ToVector(entry.State.Position), ToQuaternion(entry.State.Orientation)));
                    body.LinearVelocity = ToVector(entry.State.LinearVelocity);
                    body.AngularVelocity = ToVector(entry.State.AngularVelocity);
                }
                foreach (var entry in restored)
                {
                    if (missionRuntime == null || entry.State.Nickname is not { Length: > 0 } nickname)
                        continue;
                    missionNPCs[nickname] = entry.Object;
                    if (emitMissionSystemTransition)
                    {
                        missionRuntime.SystemExit(entry.Identity.SystemId, nickname);
                        missionRuntime.SystemEnter(World.System.Nickname, nickname);
                    }
                }
            }
            catch
            {
                foreach (var entry in restored)
                {
                    if (entry.State.Nickname is { Length: > 0 } nickname &&
                        missionNPCs.TryGetValue(nickname, out var registered) && registered == entry.Object)
                        missionNPCs.Remove(nickname);
                }
                foreach (var previousFormation in previousFormations)
                    previousFormation.Key.Formation = previousFormation.Value;
                foreach (var obj in activated)
                    World.RemoveSpawnedObjectImmediate(obj, false);
                throw;
            }
            return restored.Select(entry => entry.Object).ToArray();
        }

        private ResolvedNpcFormation[] ResolveFormations(NpcFormationStateV1[] formations,
            List<(NpcRuntimeSnapshot Identity, NpcRuntimeStateV1 State, GameObject Object)> restored)
        {
            return formations.Select(formationState =>
            {
                var members = formationState.Members.Select(member =>
                {
                    GameObject? obj = member.NpcId is { } npcId
                        ? restored.FirstOrDefault(candidate => candidate.Identity.NpcId == npcId).Object ??
                          World.GameWorld.Objects.FirstOrDefault(candidate =>
                              candidate.TryGetComponent<SNPCComponent>(out var npc) && npc.NpcId == npcId)
                        : World.Players.FirstOrDefault(candidate => candidate.Key.Character?.ID == member.CharacterId).Value;
                    return (Member: member, Object: obj ?? throw new InvalidDataException("NPC formation member could not be resolved after restore."));
                }).ToArray();
                if (members.Select(entry => entry.Object).Distinct().Count() != members.Length)
                    throw new InvalidDataException("NPC formation resolves multiple stable references to the same object.");
                return new ResolvedNpcFormation(formationState, members);
            }).ToArray();
        }

        private static void RestoreFormations(ResolvedNpcFormation[] formations)
        {
            foreach (var resolved in formations)
            {
                var formationState = resolved.State;
                var members = resolved.Members;
                var leader = members.Single(entry => entry.Member.IsLeader).Object;
                var followers = members.Where(entry => !entry.Member.IsLeader).ToArray();
                var formation = new ShipFormation(leader, followers.Select(entry => entry.Object).ToArray())
                {
                    PlayerPosition = formationState.PlayerPosition is null ? null : ToVector(formationState.PlayerPosition),
                    PlayerTargetPosition = formationState.PlayerTargetPosition is null ? null : ToVector(formationState.PlayerTargetPosition)
                };
                foreach (var entry in members)
                {
                    entry.Object.Formation = formation;
                    if (!entry.Member.IsLeader)
                        formation.SetShipOffset(entry.Object, ToVector(entry.Member.Offset));
                }
            }
        }

        private static Vector3 ToVector(NpcVector3 value) => new(value.X, value.Y, value.Z);
        private static Quaternion ToQuaternion(NpcQuaternion value) => Quaternion.Normalize(new(value.X, value.Y, value.Z, value.W));

        private void RestoreStructuralParts(GameObject obj, NpcStructuralPartStateV1[] states)
        {
            if (states.Length == 0)
                return;
            var model = obj.Model ?? throw new InvalidDataException("NPC structural damage has no target ship model.");
            foreach (var state in states.Where(part => !part.Destroyed))
            {
                var group = model.CollisionGroups.FirstOrDefault(candidate =>
                    string.Equals(candidate.ModelPart.Name, state.PartName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"NPC structural collision group '{state.PartName}' is unavailable.");
                group.CurrentHealth = group.MaxHealth * state.HealthFraction;
            }
            foreach (var state in states.Where(part => part.Destroyed))
            {
                var modelPart = model.RigidModel.AllParts.FirstOrDefault(part =>
                    string.Equals(part.Name, state.PartName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"NPC structural part '{state.PartName}' is unavailable.");
                if (modelPart.Active && !obj.DisableCmpPart(state.PartName, null, World.Server.Resources, out _))
                    throw new InvalidDataException($"NPC structural part '{state.PartName}' could not be destroyed during restore.");
            }
        }

        private GameObject? ResolveTransferReference(NpcTransferObjectReferenceV1 reference,
            List<(NpcRuntimeSnapshot Identity, NpcRuntimeStateV1 State, GameObject Object)> restored) =>
            reference.Kind switch
            {
                "npc" when reference.NpcId is Guid npcId =>
                    restored.FirstOrDefault(candidate => candidate.Identity.NpcId == npcId).Object ??
                    World.GameWorld.Objects.FirstOrDefault(candidate =>
                        candidate.TryGetComponent<SNPCComponent>(out var npc) && npc.NpcId == npcId),
                "character" when reference.CharacterId is long characterId =>
                    World.Players.FirstOrDefault(candidate => candidate.Key.Character?.ID == characterId).Value,
                "nickname" when !string.IsNullOrWhiteSpace(reference.Nickname) =>
                    World.GameWorld.GetObject(reference.Nickname),
                _ => null
            };
    }
}
