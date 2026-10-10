using System.Numerics;
using LibreLancer.Fx;
using Xunit;

namespace LibreLancer.Tests;

public class FLBeamAppearanceTests
{
    [Fact]
    public void LastBeamPointUsesIncomingSegmentDirection()
    {
        var direction = FLBeamAppearance.GetBeamForward(
            new Vector3(4, 0, 0),
            new Vector3(2, 0, 0),
            Vector3.Zero,
            hasPrevious: true,
            hasNext: false);

        Assert.Equal(Vector3.UnitX, direction);
    }

    [Fact]
    public void ZeroLengthNextSegmentUsesPreviousNonZeroTangent()
    {
        var direction = FLBeamAppearance.GetBeamForward(
            new Vector3(2, 0, 0),
            Vector3.UnitX,
            new Vector3(2, 0, 0),
            hasPrevious: true,
            hasNext: true);

        Assert.Equal(Vector3.UnitX, direction);
    }

    [Fact]
    public void FullyDegenerateBeamSegmentReturnsFiniteZeroDirection()
    {
        var direction = FLBeamAppearance.GetBeamForward(
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            hasPrevious: true,
            hasNext: true);

        Assert.Equal(Vector3.Zero, direction);
        Assert.True(float.IsFinite(direction.X));
        Assert.True(float.IsFinite(direction.Y));
        Assert.True(float.IsFinite(direction.Z));
    }
}
