using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibreLancer.Data.Schema.Missions;
using LibreLancer.World;
using Zone = LibreLancer.Data.GameData.World.Zone;

namespace LibreLancer.Server;

public partial class SpacePopulationManager
{
    private sealed class ZoneState(Zone zone)
    {
        public readonly Zone Zone = zone;
        public readonly List<PopGroup> Groups = [];
        public readonly Dictionary<EncounterFormation, int> FormationCreateCounts = [];
        public double TimeUntilHeartbeat;
        public double BattleCooldown;
        public bool InBattle => BattleCooldown > 0;
        public List<PatrolPathSegment>? Path;
        public int PathIndex = -1;
    }

    internal readonly record struct PatrolPathSegment(Zone Zone, int Label);

    private sealed class PopGroup(ZoneState state, EncounterInfo encounter)
    {
        public readonly ZoneState State = state;
        public readonly EncounterInfo Encounter = encounter;
        public readonly List<GameObject> Ships = [];
        public readonly HashSet<string> PopulationClasses = new(System.StringComparer.OrdinalIgnoreCase);
        public double AgeSeconds;
        public string? ArrivalObject;
        public float PersistDistance;
        public int PathIndex = state.PathIndex;
        public Vector3? InitialPathTarget;
        public bool PatrolPathExhausted;
        public bool InCombat;
        public bool Fleeing;
        public GameObject? AlertTarget;
        public bool IsTransferred;
        public Vector3? ResumeDutyTarget;
    }

    public JumperNpc[] GatherPopulationTransferGroup(GameObject ship)
    {
        var transferred = transferredGroups.FirstOrDefault(group => group.Contains(ship));
        if (transferred is not null)
        {
            if (transferred.Any(member => !Alive(member) ||
                    !member.TryGetComponent<Components.SNPCComponent>(out var npc) || npc.MissionRuntime is not null ||
                    npc.TerminalCheckpointPending))
                return [];
            return transferred.Select(member => JumperNpc.FromGameObject(member, world)).ToArray();
        }
        foreach (var state in zones)
        foreach (var group in state.Groups)
        {
            if (!group.Ships.Contains(ship) ||
                !CanTransferPopulationGroup(group.IsTransferred, group.Encounter?.FormationDefinition?.Behavior))
                continue;
            var members = group.Ships.Where(Alive).ToArray();
            if (members.Length == 0 || members.Any(member =>
                    !member.TryGetComponent<Components.SNPCComponent>(out var npc) || npc.MissionRuntime is not null ||
                    npc.TerminalCheckpointPending))
                return [];
            return members.Select(member => JumperNpc.FromGameObject(member, world)).ToArray();
        }
        return [];
    }

    internal static bool CanTransferPopulationGroup(bool isTransferred, EncounterBehavior? behavior) =>
        isTransferred || behavior == EncounterBehavior.trade;

    private readonly record struct SpawnLocation(
        Vector3 Position,
        Quaternion Orientation,
        string? ArrivalObject,
        int ArrivalIndex,
        int PathIndex = -1,
        Vector3? InitialPathTarget = null,
        float PersistDistance = 0);

    private readonly record struct PopulationContext(GameObject[] Players, int Density);
}
