using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.Schema.Missions;
using LibreLancer.Missions;
using LibreLancer.Missions.Directives;
using LibreLancer.Server.Components;
using LibreLancer.World;
using LibreLancer.World.Components;
using Zone = LibreLancer.Data.GameData.World.Zone;

namespace LibreLancer.Server;

public partial class SpacePopulationManager
{
    private const float PatrolPathWaypointRange = 250f;
    private const int MaxPatrolPathTargets = 4;

    private void AssignDirectives(PopGroup group)
    {
        var leader = group.Ships.FirstOrDefault(Alive);
        if (leader == null)
            return;

        var directives = BuildDirectives(group, leader.WorldTransform.Position);
        if (directives.Length == 0)
            return;

        var leadOnly = group.Encounter?.Formation != null && group.Ships.Count > 1;
        foreach (var ship in group.Ships)
        {
            if (!Alive(ship))
                continue;
            if (leadOnly && ship != leader)
                continue;

            ship.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(directives, world.GameWorld);
        }
    }

    public void AdoptTransferredTraderGroup(GameObject[] ships, string? arrivalObject)
    {
        var members = ships.Where(Alive).ToArray();
        if (members.Length == 0)
            return;
        var position = members[0].WorldTransform.Position;
        if (zones.Count == 0)
        {
            transferredGroups.Add(members);
            BindStandaloneTraderCombatReactions(members);
            var transferredLeader = members.FirstOrDefault(x => x.Formation is null || x.Formation.LeadShip == x) ?? members[0];
            if (members.Any(ship => ship.GetComponent<SNPCComponent>()?.PopulationFleeing == true))
            {
                var fleeRoute = BuildStandaloneFriendlyEscapeRoute(position,
                    transferredLeader.GetComponent<SRepComponent>()?.Faction);
                if (fleeRoute.Length > 0)
                {
                    transferredLeader.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(fleeRoute, world.GameWorld);
                    return;
                }
            }
            var initialCargoRoute = BuildTradeCargoDirectives(members, position, arrivalObject);
            if (initialCargoRoute.Length > 0)
            {
                var traderLeader = members.FirstOrDefault(x => x.Formation is null || x.Formation.LeadShip == x) ?? members[0];
                traderLeader.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(initialCargoRoute, world.GameWorld);
                return;
            }
            transferredLeader.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(
                BuildLocalWanderDirectives(position), world.GameWorld);
            return;
        }
        var state = zones.OrderBy(candidate => candidate.Zone.DistanceToEdge(position)).First();
        var group = new PopGroup(state, null!)
        {
            IsTransferred = true,
            PersistDistance = DefaultPersistDistance,
            ArrivalObject = arrivalObject
        };
        group.Ships.AddRange(members);
        group.Fleeing = members.Any(ship => ship.GetComponent<SNPCComponent>()?.PopulationFleeing == true);
        state.Groups.Add(group);
        BindTraderCombatReactions(group);
        var leader = group.Ships.FirstOrDefault(x => x.Formation is null || x.Formation.LeadShip == x) ?? group.Ships[0];
        leader.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(
            group.Fleeing
                ? BuildFriendlyEscapeRoute(group, leader.WorldTransform.Position,
                    leader.GetComponent<SRepComponent>()?.Faction)
                : BuildTradeCargoDirectives(group.Ships, leader.WorldTransform.Position, arrivalObject) is { Length: > 0 } cargoRoute
                    ? cargoRoute
                    : BuildTradeDirectives(state), world.GameWorld);
    }

    internal void AdoptRecoveredCheckpointGroup(GameObject[] ships)
    {
        var members = ships.Distinct().ToArray();
        if (members.Length == 0 || members.Any(ship => !Alive(ship) ||
                !ship.TryGetComponent<SNPCComponent>(out var npc) || npc.MissionRuntime is not null ||
                npc.NpcId == Guid.Empty || npc.TerminalCheckpointPending))
            throw new InvalidDataException("Recovered NPC checkpoint group is incomplete or ineligible.");
        var ids = members.Select(ship => ship.GetComponent<SNPCComponent>()!.NpcId).ToHashSet();
        if (transferredGroups.Any(group => group.Any(ship =>
                ship.TryGetComponent<SNPCComponent>(out var npc) && ids.Contains(npc.NpcId))))
            throw new InvalidDataException("Recovered NPC checkpoint group overlaps an active population group.");
        transferredGroups.Add(members);
        BindStandaloneTraderCombatReactions(members);
    }

    /// <summary>Returns a population group to local traffic after its gate transfer fails.</summary>
    public void ResumeFailedTraderGroup(Guid[] npcIds, string failedGate)
    {
        if (npcIds.Length == 0)
            return;

        var ids = npcIds.ToHashSet();
        var members = world.GameWorld.Objects
            .Where(obj => obj.TryGetComponent<SNPCComponent>(out var npc) && ids.Contains(npc.NpcId) && Alive(obj))
            .ToArray();
        if (members.Length != ids.Count)
            return;

        foreach (var state in zones)
        {
            for (int i = state.Groups.Count - 1; i >= 0; i--)
            {
                var group = state.Groups[i];
                group.Ships.RemoveAll(ship => ship.TryGetComponent<SNPCComponent>(out var npc) && ids.Contains(npc.NpcId));
                if (group.Ships.Count == 0)
                    state.Groups.RemoveAt(i);
            }
        }
        for (int i = transferredGroups.Count - 1; i >= 0; i--)
        {
            transferredGroups[i] = transferredGroups[i].Where(ship =>
                !ship.TryGetComponent<SNPCComponent>(out var npc) || !ids.Contains(npc.NpcId)).ToArray();
            if (transferredGroups[i].Length == 0)
                transferredGroups.RemoveAt(i);
        }

        var leader = members.FirstOrDefault(obj => obj.Formation is null || obj.Formation.LeadShip == obj) ?? members[0];
        if (zones.Count > 0)
        {
            var state = zones.OrderBy(candidate => candidate.Zone.DistanceToEdge(leader.WorldTransform.Position)).First();
            var group = new PopGroup(state, null!)
            {
                IsTransferred = true, PersistDistance = DefaultPersistDistance, ArrivalObject = failedGate
            };
            group.Ships.AddRange(members);
            group.Fleeing = members.Any(ship => ship.GetComponent<SNPCComponent>()?.PopulationFleeing == true);
            state.Groups.Add(group);
            BindTraderCombatReactions(group);
            leader.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(
                group.Fleeing
                    ? BuildFriendlyEscapeRoute(group, leader.WorldTransform.Position,
                        leader.GetComponent<SRepComponent>()?.Faction) is { Length: > 0 } escapeRoute
                        ? escapeRoute
                        : BuildWanderDirectives(state.Zone)
                    : BuildTradeCargoDirectives(group.Ships, leader.WorldTransform.Position, failedGate) is { Length: > 0 } cargoRoute
                        ? cargoRoute
                        : BuildTradeDirectives(state), world.GameWorld);
        }
        else
        {
            transferredGroups.Add(members);
            BindStandaloneTraderCombatReactions(members);
            var gate = world.GameWorld.GetObject(failedGate);
            var away = gate is null ? Vector3.UnitZ : leader.WorldTransform.Position - gate.WorldTransform.Position;
            if (away.LengthSquared() < 1)
                away = Vector3.UnitZ;
            var target = leader.WorldTransform.Position + Vector3.Normalize(away) * 5000;
            leader.GetComponent<DirectiveRunnerComponent>()?.SetDirectives(
                [new GotoVecDirective { Target = target, CruiseKind = GotoKind.GotoCruise, Range = 750, MaxThrottle = 100 }],
                world.GameWorld);
        }
    }


    private MissionDirective[] BuildDirectives(PopGroup group, Vector3 currentPosition)
    {
        if (group.Fleeing)
        {
            var escapeRoute = BuildFriendlyEscapeRoute(group, currentPosition,
                group.Ships.FirstOrDefault(Alive)?.GetComponent<SRepComponent>()?.Faction);
            return escapeRoute.Length > 0 ? escapeRoute : BuildWanderDirectives(group.State.Zone);
        }

        var retirementDockable = GetRetirementDockable(group, currentPosition);
        if (retirementDockable != null)
            return BuildRetirementDirectives(retirementDockable);

        var behavior = group.IsTransferred
            ? EncounterBehavior.trade
            : group.Encounter.FormationDefinition?.Behavior ?? EncounterBehavior.wander;
        return behavior switch
        {
            EncounterBehavior.patrol_path when group.PatrolPathExhausted => BuildWanderDirectives(group.State.Zone),
            EncounterBehavior.patrol_path => BuildPathDirectives(group, GotoKind.GotoCruise, 100, PatrolPathWaypointRange),
            EncounterBehavior.trade => BuildHybridTradeDirectives(group, currentPosition),
            _ => BuildWanderDirectives(group.State.Zone)
        };
    }

    private MissionDirective[] BuildHybridTradeDirectives(PopGroup group, Vector3 currentPosition)
    {
        var cargoRoute = BuildTradeCargoDirectives(group.Ships, currentPosition, group.ArrivalObject);
        if (cargoRoute.Length > 0)
            return cargoRoute;
        if (IsPatrol(group.State.Zone) && !group.PatrolPathExhausted)
            return BuildPathDirectives(group, GotoKind.GotoCruise, 100, PatrolPathWaypointRange);
        return BuildTradeDirectives(group.State);
    }

    private GameObject? GetRetirementDockable(PopGroup group, Vector3 currentPosition)
    {
        var reliefTime = group.State.Zone.ReliefTime;
        if (reliefTime <= 0 || group.AgeSeconds < reliefTime)
            return null;

        var behavior = group.Encounter?.FormationDefinition?.Behavior ?? EncounterBehavior.trade;
        if (behavior != EncounterBehavior.patrol_path && !IsPatrol(group.State.Zone))
            return null;

        if (HasNextPatrolPathSegment(group))
            return null;

        return FindRetirementDockable(group, currentPosition);
    }

    private static bool HasNextPatrolPathSegment(PopGroup group)
    {
        var path = group.State.Path;
        if (path == null || path.Count < 2 || group.PathIndex < 0)
            return false;

        return IsClosedPath(path) || group.PathIndex + 1 < path.Count;
    }

    private static MissionDirective[] BuildRetirementDirectives(GameObject dockable)
    {
        return
        [
            new GotoShipDirective
            {
                Target = dockable.Nickname!,
                CruiseKind = GotoKind.GotoCruise,
                Range = 750,
                MaxThrottle = 100
            },
            new DockDirective { Target = dockable.Nickname! }
        ];
    }

    private MissionDirective[] BuildWanderDirectives(Zone zone)
    {
        var count = random.Next(2, 5);
        var directives = new MissionDirective[count];
        for (int i = 0; i < directives.Length; i++)
        {
            directives[i] = new GotoVecDirective
            {
                Target = SampleZonePoint(zone),
                CruiseKind = GotoKind.GotoNoCruise,
                Range = 500,
                MaxThrottle = 80
            };
        }
        return directives;
    }

    private MissionDirective[] BuildTradeDirectives(ZoneState state)
    {
        if (IsPatrol(state.Zone) && state.Path is { Count: > 1 })
            return BuildPathDirectives(state, GotoKind.GotoCruise, 100, PatrolPathWaypointRange);
        return BuildWanderDirectives(state.Zone);
    }

    private MissionDirective[] BuildLocalWanderDirectives(Vector3 position)
    {
        var count = random.Next(2, 5);
        var directives = new MissionDirective[count];
        for (int i = 0; i < directives.Length; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var distance = random.NextSingle() * 4000 + 1000;
            directives[i] = new GotoVecDirective
            {
                Target = position + new Vector3(MathF.Cos(angle) * distance,
                    random.NextSingle() * 1000 - 500, MathF.Sin(angle) * distance),
                CruiseKind = GotoKind.GotoNoCruise,
                Range = 500,
                MaxThrottle = 80
            };
        }
        return directives;
    }

    private MissionDirective[] BuildPathDirectives(
        ZoneState state,
        GotoKind kind,
        float throttle,
        float range = 750)
    {
        var targets = GetPathTargets(state);
        if (targets.Count == 0)
            targets.Add(SampleZonePoint(state.Zone));

        return BuildGotoDirectives(targets, kind, throttle, range);
    }

    private MissionDirective[] BuildPathDirectives(
        PopGroup group,
        GotoKind kind,
        float throttle,
        float range = 750)
    {
        var targets = GetPathTargets(group);
        if (targets.Count == 0)
            targets.Add(SampleZonePoint(group.State.Zone));

        return BuildGotoDirectives(targets, kind, throttle, range);
    }

    private static MissionDirective[] BuildGotoDirectives(
        List<Vector3> targets,
        GotoKind kind,
        float throttle,
        float range)
    {
        return targets.Select(x => (MissionDirective)new GotoVecDirective
        {
            Target = x,
            CruiseKind = kind,
            Range = range,
            MaxThrottle = throttle
        }).ToArray();
    }

    private List<Vector3> GetPathTargets(ZoneState state)
    {
        var result = new List<Vector3>();
        if (state.Path == null || state.PathIndex < 0)
            return result;

        for (int i = state.PathIndex; i < state.Path.Count && result.Count < MaxPatrolPathTargets; i++)
            AddPatrolSegmentTargets(result, state.Path, i, i != state.PathIndex);

        return result;
    }

    private List<Vector3> GetPathTargets(PopGroup group)
    {
        var result = new List<Vector3>();
        var path = group.State.Path;
        if (path == null || path.Count == 0 || group.PathIndex < 0)
        {
            group.PatrolPathExhausted = true;
            return result;
        }

        if (group.InitialPathTarget is { } initialTarget)
        {
            result.Add(initialTarget);
            group.InitialPathTarget = null;
        }

        var index = Math.Clamp(group.PathIndex, 0, path.Count - 1);
        var includeCurrent = result.Count > 0 &&
                             TryGetPatrolPathLine(path, index, out _, out _);
        var closedLoop = IsClosedPath(path);

        for (int count = 0; count < MaxPatrolPathTargets && result.Count < MaxPatrolPathTargets && path.Count > 1; count++)
        {
            if (!includeCurrent)
            {
                index++;
                if (closedLoop)
                    index %= path.Count;
                else if (index >= path.Count)
                    break;
            }

            AddPatrolSegmentTargets(result, path, index, index != group.PathIndex);
            includeCurrent = false;
        }

        if (result.Count == 0)
        {
            result.Add(SampleZonePoint(group.State.Zone));
        }

        group.PathIndex = index;
        group.PatrolPathExhausted = IsPatrolPathExhausted(path.Count, index, closedLoop);
        return result;
    }

    private static void AddPatrolSegmentTargets(
        List<Vector3> result,
        List<PatrolPathSegment> path,
        int pathIndex,
        bool includeStart)
    {
        if (!TryGetPatrolPathLine(path, pathIndex, out var start, out var end))
        {
            AddPatrolTarget(result, path[pathIndex].Zone.Position);
            return;
        }

        if (includeStart && result.Count + 1 < MaxPatrolPathTargets)
            AddPatrolTarget(result, start);
        AddPatrolTarget(result, end);
    }

    private static void AddPatrolTarget(List<Vector3> result, Vector3 target)
    {
        if (result.Count >= MaxPatrolPathTargets)
            return;
        if (result.Count > 0 &&
            Vector3.DistanceSquared(result[^1], target) < PatrolPathWaypointRange * PatrolPathWaypointRange)
        {
            return;
        }
        result.Add(target);
    }

    internal static bool IsClosedPath(List<PatrolPathSegment> path)
    {
        if (path.Count < 3)
            return false;

        if (ReferenceEquals(path[0].Zone, path[^1].Zone))
            return true;

        var first = path[0].Zone.Position;
        var last = path[^1].Zone.Position;
        return Vector3.DistanceSquared(first, last) < 1;
    }

    internal static bool IsPatrolPathExhausted(int pathCount, int lastPlannedIndex, bool closedLoop) =>
        !closedLoop && (pathCount < 2 || lastPlannedIndex >= pathCount - 1);

    private GameObject? FindRetirementDockable(PopGroup group, Vector3 currentPosition)
    {
        var maxDistance = group.PersistDistance > 0
            ? group.PersistDistance
            : DefaultPersistDistance;
        var maxDistanceSquared = maxDistance * maxDistance;

        GameObject? nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var obj in world.GameWorld.Objects)
        {
            if (string.IsNullOrWhiteSpace(obj.Nickname) ||
                obj.SystemObject == null ||
                !Alive(obj) ||
                !obj.TryGetComponent<SDockableComponent>(out var dockable) ||
                dockable.Action.Kind != DockKinds.Base ||
                dockable.DockPoints.Length == 0)
            {
                continue;
            }

            var distance = Vector3.DistanceSquared(currentPosition, obj.WorldTransform.Position);
            if (distance > maxDistanceSquared || distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = obj;
        }
        return nearest;
    }

    private GameObject? FindDockable(Vector3 currentPosition, string? excludeNickname = null)
    {
        GameObject? nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var obj in world.GameWorld.Objects)
        {
            if (obj.SystemObject == null ||
                (obj.Flags & GameObjectFlags.Exists) != GameObjectFlags.Exists ||
                !obj.TryGetComponent<SDockableComponent>(out _))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(excludeNickname) &&
                excludeNickname.Equals(obj.Nickname, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var distance = Vector3.DistanceSquared(currentPosition, obj.WorldTransform.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = obj;
            }
        }
        return nearest;
    }

    private GameObject? FindPopulationJumpGate(Vector3 currentPosition, string? excludeNickname)
    {
        GameObject? nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var obj in world.GameWorld.Objects)
        {
            if (obj.SystemObject == null ||
                string.IsNullOrWhiteSpace(obj.Nickname) ||
                (obj.Flags & GameObjectFlags.Exists) != GameObjectFlags.Exists ||
                !obj.TryGetComponent<SDockableComponent>(out var dockable) ||
                !IsInterSystemPopulationJump(dockable.Action, world.System.Nickname) ||
                world.Server.GameData.Items.Systems.Get(dockable.Action.Target!) is null)
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(excludeNickname) &&
                excludeNickname.Equals(obj.Nickname, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var distance = Vector3.DistanceSquared(currentPosition, obj.WorldTransform.Position);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = obj;
        }
        return nearest;
    }

    internal static bool IsInterSystemPopulationJump(DockAction? action, string sourceSystemId) =>
        action is { Kind: DockKinds.Jump, Target.Length: > 0, Exit.Length: > 0 } &&
        !action.Target.Equals(sourceSystemId, StringComparison.OrdinalIgnoreCase);

    private Vector3 GetFirstDirectiveTarget(ZoneState state, EncounterInfo info)
    {
        var behavior = info.FormationDefinition?.Behavior ?? EncounterBehavior.wander;
        if (behavior == EncounterBehavior.patrol_path || IsPatrol(state.Zone))
        {
            var targets = GetPathTargets(state);
            if (targets.Count > 0)
                return targets[0];
        }
        return SampleZonePoint(state.Zone);
    }

    private void UpdateIdleGroupDirectives(ZoneState state)
    {
        foreach (var group in state.Groups)
        {
            if (group.InCombat)
                continue;

            var leader = group.Ships.FirstOrDefault(Alive);
            if (leader == null)
                continue;
            var runner = leader.GetComponent<DirectiveRunnerComponent>();
            if (runner is { Active: false })
                AssignDirectives(group);
        }
    }

    private void UpdateIdleTransferredGroupDirectives()
    {
        foreach (var ships in transferredGroups)
        {
            var leader = ships.FirstOrDefault(Alive);
            if (leader == null)
                continue;
            var runner = leader.GetComponent<DirectiveRunnerComponent>();
            if (runner is not { Active: false })
                continue;

            var position = leader.WorldTransform.Position;
            if (ships.Any(ship => ship.GetComponent<SNPCComponent>()?.PopulationFleeing == true))
            {
                var faction = leader.GetComponent<SRepComponent>()?.Faction;
                var fleeRoute = BuildStandaloneFriendlyEscapeRoute(position, faction);
                if (fleeRoute.Length > 0)
                {
                    runner.SetDirectives(fleeRoute, world.GameWorld);
                    continue;
                }
                runner.SetDirectives(BuildLocalWanderDirectives(position), world.GameWorld);
                continue;
            }
            var cargoRoute = BuildTradeCargoDirectives(ships, position, null);
            if (cargoRoute.Length > 0)
            {
                runner.SetDirectives(cargoRoute, world.GameWorld);
                continue;
            }
            if (zones.Count > 0)
            {
                var state = zones.OrderBy(candidate => candidate.Zone.DistanceToEdge(position)).First();
                runner.SetDirectives(BuildTradeDirectives(state), world.GameWorld);
            }
            else
                runner.SetDirectives(BuildLocalWanderDirectives(position), world.GameWorld);
        }
    }
}
