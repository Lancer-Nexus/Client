using System;
using System.Numerics;
using LibreLancer.Server.Components;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FuseImpulseTests
{
    [Fact]
    public void ImpulseFallsOffLinearlyAcrossConfiguredRadius()
    {
        var impulse = SFuseRunnerComponent.CalculateImpulse(Vector3.Zero, new Vector3(5, 0, 0), 10, 100);

        Assert.Equal(new Vector3(50, 0, 0), impulse);
    }

    [Fact]
    public void ImpulseIsZeroAtOrOutsideRadius()
    {
        Assert.Equal(Vector3.Zero,
            SFuseRunnerComponent.CalculateImpulse(Vector3.Zero, new Vector3(10, 0, 0), 10, 100));
        Assert.Equal(Vector3.Zero,
            SFuseRunnerComponent.CalculateImpulse(Vector3.Zero, new Vector3(11, 0, 0), 10, 100));
    }

    [Fact]
    public void ImpulseAtExplosionOriginIsZero()
    {
        Assert.Equal(Vector3.Zero, SFuseRunnerComponent.CalculateImpulse(Vector3.Zero, Vector3.Zero, 10, 100));
    }

    [Fact]
    public void FuseActionTimeUsesFixedOrRandomizedRange()
    {
        Assert.Equal(0.25f, SFuseRunnerComponent.ResolveActionTime(new Vector2(-1, 0.25f), 0.8f));
        Assert.Equal(0.3f, SFuseRunnerComponent.ResolveActionTime(new Vector2(0.1f, 0.9f), 0.25f), 5);
        Assert.Equal(0.9f, SFuseRunnerComponent.ResolveActionTime(new Vector2(0.1f, 0.9f), 2f));
    }

    [Fact]
    public void RandomFuseAttachmentSelectsOnlyAnExistingCandidate()
    {
        var hardpoints = new[] { "HpTurret01", "HpTurret02", "HpTurret03" };

        Assert.Null(SFuseRunnerComponent.SelectRandomHardpoint(Array.Empty<string>(), 0.5f));
        Assert.Equal("HpTurret01", SFuseRunnerComponent.SelectRandomHardpoint(hardpoints, 0));
        Assert.Equal("HpTurret02", SFuseRunnerComponent.SelectRandomHardpoint(hardpoints, 0.5f));
        Assert.Equal("HpTurret03", SFuseRunnerComponent.SelectRandomHardpoint(hardpoints, 1));
    }

    [Fact]
    public void TumbleControlsApplyRandomDirectionToConfiguredMagnitude()
    {
        var range = new Vector2(0.7f, 0.95f);
        Assert.Equal(-0.7f, SFuseRunnerComponent.ResolveTumbleThrottle(range, 0, 0));
        Assert.Equal(0.95f, SFuseRunnerComponent.ResolveTumbleThrottle(range, 1, 1));
        Assert.Equal(0, SFuseRunnerComponent.ResolveTumbleThrottle(new Vector2(-1, 0), 0.5f, 0));
    }
}
