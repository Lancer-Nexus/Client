using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using LibreLancer.Missions;
using LibreLancer.Missions.Conditions;
using LibreLancer.Server;
using LibreLancer.Server.Components;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcMissionDestructionTests
{
    private static MissionRuntime CreateRuntime()
    {
        var script = new MissionScript();
        script.Ships.Add("escort", new ScriptShip { Nickname = "escort", Labels = ["convoy"] });
        script.AvailableTriggers.Add("escort_destroyed", new ScriptedTrigger("escort_destroyed", false,
            [new Cnd_Destroyed { Label = "escort" }], []));
        var runtime = new MissionRuntime(script, new Player(null!, null!, Guid.Empty), [],
            missionNickname: "fixture");
        runtime.ObjectSpawned("escort");
        runtime.ActivateTrigger("escort_destroyed");
        return runtime;
    }

    [Fact]
    public void RestoredNpcDeathUpdatesLabelsAndCompletesDestroyedCondition()
    {
        var source = CreateRuntime();
        var target = new MissionRuntime(source.Script, new Player(null!, null!, Guid.Empty), [],
            source.CaptureTransferState());
        var world = (ServerWorld)RuntimeHelpers.GetUninitializedObject(typeof(ServerWorld));
        var removals = new ConcurrentQueue<Action>();
        typeof(ServerWorld).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world, removals);
        var npc = new GameObject { Nickname = "escort" };
        var destroyable = new SDestroyableComponent(npc, world, target);

        destroyable.Destroy(true);

        Assert.False(target.Labels["convoy"].AnyAlive());
        Assert.Equal(1, target.Labels["convoy"].DestroyedCount());
        Assert.Contains("escort_destroyed", target.CaptureTransferState().CompletedTriggers);
        Assert.Empty(target.CaptureTransferState().ActiveTriggers);
        Assert.Single(removals);
        Assert.True(source.Labels["convoy"].AnyAlive());
        Assert.Empty(source.CaptureTransferState().CompletedTriggers);
    }

    [Fact]
    public void SameNicknameInAnotherMissionDoesNotReceiveDeathEvent()
    {
        var owner = CreateRuntime();
        var other = CreateRuntime();
        var destroyable = new SDestroyableComponent(new GameObject { Nickname = "escort" }, null!, owner);
        destroyable.OnKilled!();

        Assert.False(owner.Labels["convoy"].AnyAlive());
        Assert.True(other.Labels["convoy"].AnyAlive());
        Assert.Empty(other.CaptureTransferState().CompletedTriggers);
    }

    [Fact]
    public void PopulationNpcWithoutMissionDoesNotHaveMissionCallback()
    {
        var destroyable = new SDestroyableComponent(new GameObject { Nickname = "trader" }, null!);
        Assert.Null(destroyable.OnKilled);
    }
}
