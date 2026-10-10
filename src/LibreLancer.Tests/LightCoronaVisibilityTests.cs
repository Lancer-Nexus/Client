using System;
using System.Numerics;
using LibreLancer.Render;
using Xunit;

namespace LibreLancer.Tests;

public sealed class LightCoronaVisibilityTests
{
    [Fact]
    public void MissingFlareConeLeavesLightVisibleFromEveryDirection()
    {
        Assert.Equal(1f, LightEquipRenderer.GetViewConeFade(Vector3.UnitZ, -Vector3.UnitZ, null));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(60f, 1f)]
    [InlineData(140f, 0f)]
    [InlineData(180f, 0f)]
    public void FreelancerOuterAndInnerAnglesControlVisibility(float angle, float expectedFade)
    {
        var direction = Vector3.Transform(Vector3.UnitZ,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle * MathF.PI / 180f));

        var fade = LightEquipRenderer.GetViewConeFade(Vector3.UnitZ, direction, new Vector2(140, 60));

        Assert.Equal(expectedFade, fade, 5);
    }

    [Fact]
    public void FlareConeFadesBetweenItsInnerAndOuterAngles()
    {
        var direction = Vector3.Transform(Vector3.UnitZ,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 100 * MathF.PI / 180f));

        var fade = LightEquipRenderer.GetViewConeFade(Vector3.UnitZ, direction, new Vector2(140, 60));

        Assert.InRange(fade, 0.4f, 0.55f);
    }

    [Fact]
    public void DegenerateDirectionDoesNotCreateANonFiniteFade()
    {
        var fade = LightEquipRenderer.GetViewConeFade(Vector3.Zero, Vector3.UnitZ, new Vector2(140, 60));

        Assert.Equal(1f, fade);
    }
}
