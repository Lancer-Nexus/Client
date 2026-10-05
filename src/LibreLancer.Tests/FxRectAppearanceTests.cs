using System.Numerics;
using LibreLancer.Fx;
using Xunit;

namespace LibreLancer.Tests;

public class FxRectAppearanceTests
{
    [Fact]
    public void ZeroVelocityKeepsEmitterFacingNormal()
    {
        var normal = FxRectAppearance.GetNormal(Vector3.Zero, new Vector3(0, 2, 0));
        Assert.Equal(Vector3.UnitY, normal);
    }

    [Fact]
    public void ZeroVelocityAndNormalUseFiniteDefaultDirection()
    {
        var normal = FxRectAppearance.GetNormal(Vector3.Zero, Vector3.Zero);
        Assert.Equal(Vector3.UnitY, normal);
    }

    [Fact]
    public void MovingRectFollowsParticleVelocity()
    {
        var normal = FxRectAppearance.GetNormal(new Vector3(0, 0, -3), Vector3.UnitY);
        Assert.Equal(-Vector3.UnitZ, normal);
    }

    [Fact]
    public void ViewingAngleFadeIsFullWhenViewedAcrossParticleDirection()
    {
        var fade = FxBasicAppearance.GetViewingAngleFade(Vector3.UnitZ, Vector3.UnitX);

        Assert.Equal(1f, fade);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 0, -1)]
    public void ViewingAngleFadeRemovesEdgeOnParticles(float x, float y, float z)
    {
        var fade = FxBasicAppearance.GetViewingAngleFade(Vector3.UnitZ, new Vector3(x, y, z));

        Assert.Equal(0f, fade);
    }

    [Fact]
    public void ViewingAngleFadeKeepsParticlesVisibleWhenDirectionIsUnavailable()
    {
        Assert.Equal(1f, FxBasicAppearance.GetViewingAngleFade(Vector3.Zero, Vector3.UnitX));
        Assert.Equal(1f, FxBasicAppearance.GetViewingAngleFade(Vector3.UnitZ, Vector3.Zero));
    }
}
