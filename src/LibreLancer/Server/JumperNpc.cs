using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using LancerNexus.Protocol;
using MessagePack;
using System.Text.Json;
using LibreLancer.Data.GameData;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.Schema.Pilots;
using LibreLancer.Missions;
using LibreLancer.Net.Protocol;
using LibreLancer.Server.Components;
using LibreLancer.World;
using LibreLancer.World.Components;
using Pilot = LibreLancer.Data.GameData.Pilot;

namespace LibreLancer.Server;

public class JumperNpc
{
    public Guid NpcId;
    public long OwnershipVersion;
    public NpcRuntimeSnapshot RuntimeSnapshot = null!;
    public string Nickname = null!;
    public ObjectName Name = null!;
    public Faction Faction = null!;

    public uint[] DestroyedParts = [];
    public SpawnedEffect[] Effects = null!;
    public CostumeEntry SpaceCostume = null!;
    public float Health;
    public ObjectLoadout Loadout = null!;
    public Pilot Pilot = null!;
    public StateGraph StateGraph = null!;
    public uint SourceFormationId;
    public NpcFormationStateV1? FormationSnapshot;

    public static JumperNpc FromGameObject(GameObject go, ServerWorld world)
    {
        var npcComponent = go.GetComponent<SNPCComponent>()
            ?? throw new InvalidOperationException("A transferable NPC needs an SNPCComponent identity.");
        var npc = new JumperNpc
        {
            NpcId = npcComponent.NpcId,
            OwnershipVersion = npcComponent.OwnershipVersion,
            Nickname = go.Nickname!,
            Name = go.Name!,
            SpaceCostume = new()
        };

        var ld = new ObjectLoadout
        {
            Archetype = go.GetComponent<ShipComponent>()!.Ship.Nickname
        };

        if (go.TryGetComponent<SRepComponent>(out var srep))
        {
            npc.Faction = srep.Faction!;
        }

        if (go.TryGetComponent<SNPCComponent>(out var snpc))
        {
            npc.Pilot = snpc.Pilot!;
            npc.StateGraph = snpc.StateGraph!;
            npc.SpaceCostume.Head = snpc.CommHead;
            npc.SpaceCostume.Body = snpc.CommBody;
            npc.SpaceCostume.Accessory = snpc.CommHelmet;
        }
        if (go.TryGetComponent<SHealthComponent>(out var health))
        {
            npc.Health = health.CurrentHealth;
        }

        foreach (var item in go.GetComponents<EquipmentComponent>())
        {
            ld.Items.Add(item.GetLoadoutItem());
        }

        foreach (var item in go.GetChildComponents<EquipmentComponent>())
        {
            ld.Items.Add(item.GetLoadoutItem());
        }

        if (go.TryGetComponent<SNPCCargoComponent>(out var cargo))
        {
            foreach (var cg in cargo.Cargo)
            {
                ld.Cargo.Add(new BasicCargo(cg.Item, cg.Count));
            }
        }

        npc.Loadout = ld;
        var transform = go.WorldTransform;
        if (go.Formation is { } formation)
        {
            npc.SourceFormationId = formation.ID;
            npc.FormationSnapshot = new NpcFormationStateV1
            {
                Members = new[] { formation.LeadShip }.Concat(formation.Followers)
                    .Select(member => new NpcFormationMemberV1
                    {
                        IsLeader = member == formation.LeadShip,
                        NpcId = GetNpcId(member),
                        CharacterId = GetCharacterId(member),
                        Offset = ToProtocol(formation.GetShipOffset(member))
                    }).ToArray(),
                PlayerPosition = formation.PlayerPosition is { } playerPosition ? ToProtocol(playerPosition) : null,
                PlayerTargetPosition = formation.PlayerTargetPosition is { } playerTarget ? ToProtocol(playerTarget) : null
            };
        }
        var physics = go.PhysicsComponent?.Body;
        var healthComponent = go.GetComponent<SHealthComponent>();
        AutopilotTransferState? autopilot = null;
        try
        {
            autopilot = go.GetComponent<AutopilotComponent>()?.CaptureTransferState(
                SNPCComponent.CaptureObjectReference);
        }
        catch (NotSupportedException) { }
        var directiveTarget = snpc?.CurrentDirective switch
        {
            LibreLancer.Server.Ai.AiAttackState attack => attack.Target,
            LibreLancer.Server.Ai.AiDockState dock => dock.Target,
            _ => null
        };
        var aiTargetId = directiveTarget != null &&
                         directiveTarget.TryGetComponent<SNPCComponent>(out var targetNpc) && targetNpc.NpcId != Guid.Empty
            ? targetNpc.NpcId
            : (Guid?)null;
        var runtime = new NpcRuntimeStateV1
        {
            Position = ToProtocol(transform.Position),
            Orientation = new NpcQuaternion
            {
                X = transform.Orientation.X, Y = transform.Orientation.Y,
                Z = transform.Orientation.Z, W = transform.Orientation.W
            },
            LinearVelocity = ToProtocol(physics?.LinearVelocity ?? Vector3.Zero),
            AngularVelocity = ToProtocol(physics?.AngularVelocity ?? Vector3.Zero),
            Health = healthComponent?.CurrentHealth ?? 0,
            LoadoutArchetype = ld.Archetype,
            Equipment = go.GetComponents<EquipmentComponent>().Concat(go.GetChildComponents<EquipmentComponent>())
                .Select(equipment => new NpcEquipmentState
                {
                    EquipmentId = equipment.Equipment.Nickname,
                    Hardpoint = equipment.Parent.Attachment?.Name ?? "internal",
                    Health = equipment.Parent.Attachment != null &&
                             equipment.Parent.TryGetComponent<SHealthComponent>(out var equipmentHealth) &&
                             equipmentHealth.MaxHealth > 0
                        ? Math.Clamp(equipmentHealth.CurrentHealth / equipmentHealth.MaxHealth, 0, 1)
                        : 1,
                    Energy = equipment.Equipment is LibreLancer.Data.GameData.Items.PowerEquipment powerEquipment &&
                             equipment.Parent.TryGetComponent<PowerCoreComponent>(out var powerCore) &&
                             powerEquipment.Def.Capacity > 0
                        ? Math.Clamp(powerCore.CurrentEnergy / powerEquipment.Def.Capacity, 0, 1)
                        : 1,
                    ExtensionData = CaptureEquipmentExtension(equipment, world.GameWorld)
                }).ToArray(),
            Cargo = go.TryGetComponent<SNPCCargoComponent>(out var cargoComponent)
                ? cargoComponent.Cargo.Select(cargo => new NpcCargoState
                {
                    ItemId = cargo.Item.Nickname,
                    Count = cargo.Count,
                    Hardpoint = cargo.Hardpoint
                }).ToArray()
                : [],
            Autopilot = new NpcAutopilotState
            {
                Behavior = autopilot?.Behavior.ToString() ?? "unsupported",
                TargetNpcId = autopilot?.TargetNpcId,
                TargetPosition = autopilot is { Behavior: AutopilotBehaviors.Goto }
                    ? ToProtocol(autopilot.TargetPosition)
                    : null,
                Throttle = autopilot?.MaxThrottle ?? 0,
                Cruise = autopilot?.CanCruise ?? false,
                ExtensionData = autopilot is null ? [] : JsonSerializer.SerializeToUtf8Bytes(autopilot)
            },
            Ai = new NpcAiState
            {
                StateId = snpc?.CurrentDirective?.GetType().Name ?? "none",
                PreviousStateId = snpc?.TransferPreviousDirectiveId ?? "none",
                StateElapsedSeconds = snpc?.TransferDirectiveElapsedSeconds ?? 0,
                Timers = [Math.Max(0, snpc?.TransferMissileTimer ?? 0)],
                RandomState = snpc?.TransferRandomState ?? 0,
                ExtensionData = snpc is null ? [] : JsonSerializer.SerializeToUtf8Bytes(snpc.CaptureTransferState())
            },
            CurrentTargetNpcId = aiTargetId,
            Nickname = npc.Nickname,
            DisplayName = go.Name?.GetName(world.Server.GameData, transform.Position) ?? npc.Nickname,
            FactionId = npc.Faction?.Nickname ?? "",
            PilotId = npc.Pilot?.Nickname ?? "",
            StateGraphId = npc.StateGraph?.Description?.Name ?? "",
            CommHeadId = snpc?.CommHead?.Nickname ?? "",
            CommBodyId = snpc?.CommBody?.Nickname ?? "",
            CommAccessoryId = snpc?.CommHelmet?.Nickname ?? "",
            MissionState = snpc?.MissionRuntime?.Player.Character is { } missionCharacter
                ? JsonSerializer.SerializeToUtf8Bytes(new MissionNpcTransferContext(missionCharacter.ID,
                    snpc.MissionRuntime.MissionNickname))
                : [],
            StructuralParts = CaptureStructuralParts(go)
        };
        npc.RuntimeSnapshot = new NpcRuntimeSnapshot
        {
            NpcId = npcComponent.NpcId,
            OwnershipVersion = npcComponent.OwnershipVersion,
            SystemId = world.System.Nickname,
            RuntimeState = MessagePackSerializer.Serialize(runtime)
        };
        return npc;
    }

    private static byte[] CaptureEquipmentExtension(EquipmentComponent equipment, GameWorld world)
    {
        var hasShield = equipment.Parent.TryGetComponent<SShieldComponent>(out var shield);
        var hasWeapon = equipment.Parent.TryGetComponent<WeaponComponent>(out var weapon);
        var hasPowerCore = equipment.Parent.TryGetComponent<PowerCoreComponent>(out var powerCore);
        if (!hasShield && !hasWeapon && !hasPowerCore)
            return [];
        return JsonSerializer.SerializeToUtf8Bytes(new NpcEquipmentTransferExtensionV1
        {
            Shield = hasShield ? shield.CaptureTransferState(world) : null,
            WeaponCooldownSeconds = hasWeapon ? weapon.CurrentCooldown : null,
            WeaponAngles = hasWeapon
                ? new NpcWeaponAnglesTransferStateV1(weapon.Angles.X, weapon.Angles.Y)
                : null,
            ThrustCapacityFraction = hasPowerCore
                ? powerCore.Equip.ThrustCapacity > 0
                    ? Math.Clamp(powerCore.CurrentThrustCapacity / powerCore.Equip.ThrustCapacity, 0, 1)
                    : 0
                : null
        });
    }

    private static NpcStructuralPartStateV1[] CaptureStructuralParts(GameObject go)
    {
        if (go.Model is not { } model)
            return [];

        var parts = new Dictionary<string, NpcStructuralPartStateV1>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in model.CollisionGroups)
        {
            if (group.ModelPart.Active && group.HealthFraction >= 1)
                continue;
            var destroyed = !group.ModelPart.Active || group.HealthFraction <= 0;
            parts[group.ModelPart.Name] = new NpcStructuralPartStateV1
            {
                PartName = group.ModelPart.Name,
                HealthFraction = destroyed ? 0 : group.HealthFraction,
                Destroyed = destroyed
            };
        }
        foreach (var part in model.RigidModel.AllParts.Where(part => !part.Active))
        {
            parts[part.Name] = new NpcStructuralPartStateV1
            {
                PartName = part.Name,
                HealthFraction = 0,
                Destroyed = true
            };
        }
        return parts.Values.OrderBy(part => part.PartName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static NpcVector3 ToProtocol(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };

    private static Guid? GetNpcId(GameObject obj) => obj.TryGetComponent<SNPCComponent>(out var npc)
        ? npc.NpcId != Guid.Empty ? npc.NpcId : throw new InvalidDataException("Formation member NPC has no Coordinator-issued identity.")
        : null;

    private static long? GetCharacterId(GameObject obj) => obj.TryGetComponent<SPlayerComponent>(out var player)
        ? player.Player.Character is { ID: > 0 } character ? character.ID : throw new InvalidDataException("Formation player has no stable character identity.")
        : null;

    private sealed record MissionNpcTransferContext(long CharacterId, string MissionNickname);
}
