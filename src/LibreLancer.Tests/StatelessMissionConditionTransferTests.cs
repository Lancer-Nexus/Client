using System;
using LibreLancer.Missions;
using LibreLancer.Missions.Conditions;
using LancerNexus.Protocol;
using Xunit;

namespace LibreLancer.Tests;

public class StatelessMissionConditionTransferTests
{
    [Fact]
    public void TimerHasExplicitEmptyStorageBeforeFirstUpdate()
    {
        var condition = new ActiveCondition(null!, new Cnd_Timer());
        var saved = condition.Storage.CaptureTransferState();
        Assert.Equal("none", saved.Kind);
        var restored = new ActiveCondition(null!, new Cnd_Timer());
        restored.Storage.RestoreTransferState(saved);
        Assert.Equal("none", restored.Storage.CaptureTransferState().Kind);
    }

    [Fact]
    public void EmptyStorageRejectsStateFromAnotherConditionKind()
    {
        var condition = new ActiveCondition(null!, new Cnd_Timer());
        Assert.Throws<InvalidOperationException>(() => condition.Storage.RestoreTransferState(
            new NpcMissionConditionState { Kind = "double", NumberValue = 600 }));
    }
}
