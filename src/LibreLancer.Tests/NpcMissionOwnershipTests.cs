using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using LancerNexus.Protocol;
using LibreLancer.Missions;
using LibreLancer.Server;
using LibreLancer.Server.Components;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcMissionOwnershipTests
{
    private static MissionRuntime Runtime(long id)
    {
        var character = new NetCharacter();
        typeof(NetCharacter).GetField("charId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(character, id);
        var player = new Player(null!, null!, Guid.Empty) { Character = character };
        var script = new MissionScript();
        script.Ships.Add("escort", new ScriptShip { Nickname = "escort", Jumper = true });
        return new MissionRuntime(script, player, [], missionNickname: "fixture");
    }

    private static ServerWorld World(out ConcurrentQueue<Action> queue)
    {
        var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
        world.GameWorld = new GameWorld(null, null, null, null, initPhys: false);
        queue = new ConcurrentQueue<Action>();
        typeof(ServerWorld).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world, queue);
        return world;
    }

    private static GameObject Npc(ServerWorld world, MissionRuntime runtime, Guid id, bool active = true)
    {
        var obj = new GameObject { Nickname = "escort", Flags = active ? GameObjectFlags.Exists : 0 };
        obj.AddComponent(new SNPCComponent(obj, null!, null!) { MissionRuntime = runtime, NpcId = id });
        world.GameWorld.AddObject(obj);
        return obj;
    }

    [Fact]
    public void LookupSelectsOnlyActiveNpcFromTheExactRuntime()
    {
        var world = World(out _);
        var first = Runtime(1);
        var second = Runtime(2);
        var wrong = Npc(world, second, Guid.NewGuid());
        var inactive = Npc(world, first, Guid.NewGuid(), false);
        var expected = Npc(world, first, Guid.NewGuid());
        Assert.Same(expected, world.FindActiveMissionNpc(first, "ESCORT"));
        Assert.Same(wrong, world.FindActiveMissionNpc(second, "escort"));
        Assert.NotSame(inactive, world.FindActiveMissionNpc(first, "escort"));
    }

    [Fact]
    public void GatherCannotSelectAnotherPlayersJumperWithTheSameNickname()
    {
        var world = World(out _);
        Npc(world, Runtime(2), Guid.NewGuid());
        Assert.Empty(world.GatherJumpers(Runtime(1)));
    }

    [Fact]
    public void DuplicateActorsInTheSameRuntimeAreRejected()
    {
        var world = World(out _);
        var runtime = Runtime(1);
        Npc(world, runtime, Guid.NewGuid());
        Npc(world, runtime, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => world.FindActiveMissionNpc(runtime, "escort"));
    }

    [Fact]
    public async Task FreezeRejectsAnotherRuntimeBeforeCapturingOrRemovingNpc()
    {
        var world = World(out var queue);
        var id = Guid.NewGuid();
        var npc = Npc(world, Runtime(2), id);
        var freeze = world.FreezeJumpersAsync([new JumperNpc { NpcId = id, Nickname = "escort" }], Runtime(1));
        Assert.True(queue.TryDequeue(out var action));
        action();
        await Assert.ThrowsAsync<InvalidOperationException>(() => freeze);
        Assert.Same(npc, world.FindActiveNpcById(id));
        Assert.Empty(queue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RestoreRejectsWrongOrMissingMissionOwnerBeforeCreatingAnyNpc(bool provideRuntime)
    {
        var world = World(out _);
        world.System = new LibreLancer.Data.GameData.World.StarSystem { SourceFile = "fixture.ini", Nickname = "li03" };
        var manager = (NPCManager)RuntimeHelpers.GetUninitializedObject(typeof(NPCManager));
        manager.World = world;
        var owner = Runtime(1);
        var id = Guid.NewGuid();
        var transfer = Guid.NewGuid();
        var state = new NpcRuntimeStateV1
        {
            Nickname = "escort", LoadoutArchetype = "fixture_ship",
            Orientation = new NpcQuaternion { W = 1 },
            Ai = new NpcAiState { StateId = "none", PreviousStateId = "none" },
            MissionState = JsonSerializer.SerializeToUtf8Bytes(new { CharacterId = 2, MissionNickname = "fixture" })
        };
        var snapshot = new NpcTransferSnapshot
        {
            TransferId = transfer, MissionRuntimeId = transfer, TargetSystemId = "li03", NpcIds = [id],
            MissionRuntimeState = MessagePack.MessagePackSerializer.Serialize(owner.CaptureTransferState()),
            Npcs = [new NpcRuntimeSnapshot { NpcId = id, OwnershipVersion = 1, SystemId = "li01",
                RuntimeState = MessagePack.MessagePackSerializer.Serialize(state) }]
        };
        Assert.Throws<InvalidDataException>(() => manager.RestoreTransfer(snapshot, provideRuntime ? owner : null));
        Assert.Empty(world.GameWorld.Objects);
    }

    [Theory]
    [InlineData(1, "fixture", true)]
    [InlineData(1, "FIXTURE", true)]
    [InlineData(2, "fixture", false)]
    [InlineData(1, "other", false)]
    [InlineData(0, "fixture", false)]
    public void SnapshotContextRequiresTheSameCharacterAndMission(long id, string mission, bool accepted)
    {
        var state = new NpcRuntimeStateV1 { Nickname = "escort",
            MissionState = JsonSerializer.SerializeToUtf8Bytes(new { CharacterId = id, MissionNickname = mission }) };
        if (accepted)
            GameServer.ValidateMissionNpcContext(Runtime(1), state);
        else
            Assert.Throws<InvalidDataException>(() => GameServer.ValidateMissionNpcContext(Runtime(1), state));
    }
}
