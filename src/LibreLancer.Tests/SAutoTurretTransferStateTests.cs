using System.IO;
using LibreLancer.Data.Schema.Pilots;
using LibreLancer.Server;
using LibreLancer.Server.Components;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public sealed class SAutoTurretTransferStateTests
{
    [Fact]
    public void BurstTimerStartsBurstThenFiresAtTheConfiguredInterval()
    {
        var settings = new GunBlock
        {
            AutoTurretBurstIntervalTime = 1,
            AutoTurretIntervalTime = 0.1f
        };
        var turret = new SAutoTurretComponent(new GameObject(), () => settings);

        Assert.False(turret.RunFireTimer(0.01f));
        Assert.True(turret.InBurst);
        Assert.True(turret.RunFireTimer(0.01f));
        Assert.True(turret.RunFireTimer(0.1f));
    }

    [Fact]
    public void RandomizedBurstResumesFromTransferredRandomState()
    {
        var settings = new GunBlock
        {
            AutoTurretBurstIntervalTime = 2,
            AutoTurretBurstIntervalVariancePercent = 0.5f
        };
        var first = new SAutoTurretComponent(new GameObject(), () => settings);
        var restored = new SAutoTurretComponent(new GameObject(), () => settings);
        var saved = new NpcAutoTurretTransferStateV1
        {
            BurstTimer = 0,
            FireTimer = 0,
            InBurst = false,
            RandomState = 0x123456789abcdef0UL
        };
        first.RestoreTransferState(saved);
        restored.RestoreTransferState(saved);

        Assert.False(first.RunFireTimer(0.01f));
        Assert.False(restored.RunFireTimer(0.01f));
        Assert.Equal(first.CaptureTransferState(), restored.CaptureTransferState());
    }

    [Fact]
    public void InvalidTransferredRandomStateIsRejected()
    {
        var state = new NpcAutoTurretTransferStateV1
        {
            BurstTimer = 0,
            FireTimer = 0,
            RandomState = 0
        };

        Assert.Throws<InvalidDataException>(state.Validate);
    }
}
