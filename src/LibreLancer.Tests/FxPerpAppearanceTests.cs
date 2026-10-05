using System;
using System.Numerics;
using LibreLancer.Fx;
using LibreLancer.Utf.Ale;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FxPerpAppearanceTests
{
    [Fact]
    public void ZeroVelocityUsesTheEmitterDirection()
    {
        Assert.Equal(Vector3.UnitX, FxPerpAppearance.GetDirection(Vector3.Zero, new Vector3(3, 0, 0)));
    }

    [Fact]
    public void MissingVelocityAndEmitterDirectionUseFiniteDefault()
    {
        Assert.Equal(Vector3.UnitY, FxPerpAppearance.GetDirection(Vector3.Zero, Vector3.Zero));
    }

    [Fact]
    public void MovingParticleDirectionTakesPrecedenceOverFallback()
    {
        Assert.Equal(-Vector3.UnitZ, FxPerpAppearance.GetDirection(new Vector3(0, 0, -4), Vector3.UnitX));
    }

    [Fact]
    public void AnimatedOrientationRotatesParticleOffset()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);

        var position = FxPerpAppearance.TransformPosition(Vector3.UnitX, rotation, Matrix4x4.Identity);

        Assert.InRange(Vector3.Distance(position, Vector3.UnitY), 0, 0.00001f);
    }

    [Fact]
    public void AnimatedOrientationRotatesParticleDirectionAndEmitterFallback()
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);

        var velocity = FxPerpAppearance.TransformDirection(Vector3.UnitX, rotation, Matrix4x4.Identity);
        var fallback = FxPerpAppearance.TransformDirection(Vector3.UnitY, rotation, Matrix4x4.Identity);

        Assert.InRange(Vector3.Distance(velocity, Vector3.UnitY), 0, 0.00001f);
        Assert.InRange(Vector3.Distance(fallback, -Vector3.UnitX), 0, 0.00001f);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsViewingAngleFadeFromAleNode(bool enabled)
    {
        var node = new AlchemyNode();
        node.Parameters.Add(new AleParameter(AleProperty.RectApp_ViewingAngleFade, enabled));

        var appearance = new FxPerpAppearance(node);

        Assert.Equal(enabled, appearance.ViewingAngleFade);
    }
}
