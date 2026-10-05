using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using LibreLancer.Data.GameData.World;
using LibreLancer.World;
using LibreLancer.Data.Schema.Missions;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcPopulationTransferTests
{
    [Fact]
    public void TransferredTraderGroupsRemainEligibleForTheirNextJump()
    {
        Assert.True(SpacePopulationManager.CanTransferPopulationGroup(isTransferred: true, behavior: null));
    }

    [Fact]
    public void OnlyTradeEncountersCanStartAutonomousPopulationTransfers()
    {
        Assert.True(SpacePopulationManager.CanTransferPopulationGroup(
            isTransferred: false, behavior: EncounterBehavior.trade));
        Assert.False(SpacePopulationManager.CanTransferPopulationGroup(
            isTransferred: false, behavior: EncounterBehavior.wander));
        Assert.False(SpacePopulationManager.CanTransferPopulationGroup(
            isTransferred: false, behavior: null));
    }

    [Fact]
    public void PopulationRouteSelectsOnlyInterSystemJumpActionsWithAnExit()
    {
        Assert.True(SpacePopulationManager.IsInterSystemPopulationJump(
            new() { Kind = LibreLancer.Data.GameData.World.DockKinds.Jump, Target = "li03", Exit = "Li03_to_Li01" },
            "li01"));
        Assert.False(SpacePopulationManager.IsInterSystemPopulationJump(
            new() { Kind = LibreLancer.Data.GameData.World.DockKinds.Jump, Target = "li01", Exit = "Li01_to_Li02" },
            "LI01"));
        Assert.False(SpacePopulationManager.IsInterSystemPopulationJump(
            new() { Kind = LibreLancer.Data.GameData.World.DockKinds.Jump, Target = "li03", Exit = null },
            "li01"));
        Assert.False(SpacePopulationManager.IsInterSystemPopulationJump(
            new() { Kind = LibreLancer.Data.GameData.World.DockKinds.Base, Target = "li03", Exit = "Li03_to_Li01" },
            "li01"));
    }

    [Fact]
    public void UnobservedTransferredShipsKeepTheirWorldAliveUntilTheyLeave()
    {
        // No resource/physics initialization is needed to exercise population lifetime.
        var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
        world.System = new StarSystem { SourceFile = "test.ini" };
        world.Players = new();
        var population = new SpacePopulationManager(world);
        var groups = (List<GameObject[]>)typeof(SpacePopulationManager)
            .GetField("transferredGroups", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(population)!;
        var ship = new GameObject { Flags = GameObjectFlags.Exists };
        groups.Add([ship]);

        Assert.True(population.HasActiveTransferredShips);
        population.Update(3);
        Assert.True(population.HasActiveTransferredShips);
        Assert.Single(groups);

        ship.Flags = 0;
        population.Update(3);
        Assert.False(population.HasActiveTransferredShips);
        Assert.Empty(groups);
    }

    [Fact]
    public void NpcPeerAddressUsesAdvertisedPortAndPreservesLegacyGamePortFallback()
    {
        var advertised = GameServer.ParseNpcPeerEndpoint("udp://127.0.0.3:25444", 26455,
            "quic://127.0.0.3:26456");
        Assert.Equal("127.0.0.3", advertised.Host);
        Assert.Equal(26456, advertised.Port);
        Assert.Equal(26455, GameServer.ParseNpcPeerEndpoint("quic://127.0.0.3:25444", 26455).Port);
        Assert.Equal(26455, GameServer.ParseNpcPeerEndpoint("127.0.0.3:25444", 26455).Port);
        Assert.Throws<System.IO.InvalidDataException>(() =>
            GameServer.ParseNpcPeerEndpoint("127.0.0.3:25444", 26455, "quic://host"));
    }

    [Fact]
    public void AmbientFormationCreationHandlesTransferredGroupsWithoutAnEncounter()
    {
        var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
        world.System = new StarSystem { SourceFile = "test.ini" };
        var population = new SpacePopulationManager(world);
        var stateType = typeof(SpacePopulationManager).GetNestedType("ZoneState", BindingFlags.NonPublic)!;
        var groupType = typeof(SpacePopulationManager).GetNestedType("PopGroup", BindingFlags.NonPublic)!;
        var state = System.Activator.CreateInstance(stateType, new Zone())!;
        var groups = (System.Collections.IList)stateType.GetField("Groups")!.GetValue(state)!;
        var transferred = System.Activator.CreateInstance(groupType, state, null)!;
        ((List<GameObject>)groupType.GetField("Ships")!.GetValue(transferred)!)
            .Add(new GameObject { Flags = GameObjectFlags.Exists });
        groups.Add(transferred);
        var formation = new EncounterFormation { AllowSimultaneousCreation = false };
        var info = new EncounterInfo { FormationDefinition = formation };
        var canCreate = typeof(SpacePopulationManager).GetMethod("CanCreateFormation",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)canCreate.Invoke(population, [state, info])!);
        var ambient = System.Activator.CreateInstance(groupType, state, info)!;
        ((List<GameObject>)groupType.GetField("Ships")!.GetValue(ambient)!)
            .Add(new GameObject { Flags = GameObjectFlags.Exists });
        groups.Add(ambient);
        Assert.False((bool)canCreate.Invoke(population, [state, info])!);
    }

#if DEBUG
    [Fact]
    public async System.Threading.Tasks.Task LocalNpcDiagnosticsCaptureOnlyActiveCopiesOnTheSimulationQueue()
    {
        var server = (GameServer)RuntimeHelpers.GetUninitializedObject(typeof(GameServer));
        server.InstanceId = "li01";
        var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
        world.System = new StarSystem { SourceFile = "test.ini", Nickname = "li01" };
        world.GameWorld = new GameWorld(null, null, null, null, initPhys: false);
        var id = System.Guid.NewGuid();
        var ship = new GameObject { Flags = GameObjectFlags.Exists, Nickname = "trader" };
        var script = new LibreLancer.Missions.MissionScript();
        script.AvailableTriggers.Add("timer", new LibreLancer.Missions.ScriptedTrigger("timer", false,
            [new LibreLancer.Missions.Conditions.Cnd_Timer { Seconds = 600 }], []));
        var mission = new LibreLancer.Missions.MissionRuntime(script, new Player(null!, null!, System.Guid.Empty), [],
            new LancerNexus.Protocol.NpcMissionRuntimeStateV1
            {
                MissionNickname = "fixture", RandomState = 123,
                ActiveTriggers = [new() { Nickname = "timer", ActiveSeconds = 12.5, Satisfied = new byte[16],
                    Conditions = [new() { Kind = "none" }] }]
            });
        ship.AddComponent(new LibreLancer.Server.Components.SNPCComponent(ship, null!, null!)
            { NpcId = id, OwnershipVersion = 6, MissionRuntime = mission });
        world.GameWorld.AddObject(ship);
        var inactive = new GameObject { Flags = 0, Nickname = "frozen" };
        inactive.AddComponent(new LibreLancer.Server.Components.SNPCComponent(inactive, null!, null!)
            { NpcId = id, OwnershipVersion = 5 });
        world.GameWorld.AddObject(inactive);
        var worlds = new Dictionary<StarSystem, ServerWorld>();
        typeof(GameServer).GetField("worlds", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(server, worlds);
        worlds.Add(world.System, world);
        var queue = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();
        typeof(GameServer).GetField("worldRequests", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(server, queue);
        var task = server.CaptureNpcDiagnosticsAsync(id);
        Assert.False(task.IsCompleted);
        Assert.True(queue.TryDequeue(out var capture));
        capture();
        using var json = System.Text.Json.JsonDocument.Parse(await task);
        Assert.Equal("li01", json.RootElement.GetProperty("InstanceId").GetString());
        var active = Assert.Single(json.RootElement.GetProperty("Npcs").EnumerateArray());
        Assert.Equal(id, active.GetProperty("NpcId").GetGuid());
        Assert.Equal(6, active.GetProperty("OwnershipVersion").GetInt64());
        Assert.Equal("trader", active.GetProperty("Nickname").GetString());
        var missionState = active.GetProperty("MissionState");
        Assert.Equal("fixture", missionState.GetProperty("MissionNickname").GetString());
        Assert.Equal(123ul, missionState.GetProperty("RandomState").GetUInt64());
        Assert.Equal(12.5, Assert.Single(missionState.GetProperty("ActiveTriggers").EnumerateArray())
            .GetProperty("ActiveSeconds").GetDouble());
    }
#endif

    [Fact]
    public void PopulationNamesDoNotCollideAcrossWorldRestartsOrInstances()
    {
        var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
        world.System = new StarSystem { SourceFile = "test.ini" };
        var source = new SpacePopulationManager(world);
        var restarted = new SpacePopulationManager(world);
        var first = source.NextPopulationNickname(0);
        Assert.NotEqual(first, source.NextPopulationNickname(0));
        Assert.NotEqual(first, restarted.NextPopulationNickname(0));
        Assert.StartsWith("spacepop_", first);
        Assert.True(first.Length <= 96);
    }

    [Fact]
    public void FreezeLookupUsesStableNpcIdWhenInternalNamesCollide()
    {
        var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
        world.GameWorld = new GameWorld(null, null, null, null, initPhys: false);
        var expectedId = System.Guid.NewGuid();
        var other = new GameObject { Flags = GameObjectFlags.Exists, Nickname = "spacepop_35_0" };
        other.AddComponent(new LibreLancer.Server.Components.SNPCComponent(other, null!, null!)
            { NpcId = System.Guid.NewGuid(), OwnershipVersion = 1 });
        var restored = new GameObject { Flags = GameObjectFlags.Exists, Nickname = other.Nickname };
        restored.AddComponent(new LibreLancer.Server.Components.SNPCComponent(restored, null!, null!)
            { NpcId = expectedId, OwnershipVersion = 6 });
        world.GameWorld.AddObject(other);
        world.GameWorld.AddObject(restored);
        Assert.Same(restored, world.FindActiveNpcById(expectedId));
        restored.Flags = 0;
        Assert.Null(world.FindActiveNpcById(expectedId));
        Assert.Null(world.FindActiveNpcById(System.Guid.Empty));
    }

}
