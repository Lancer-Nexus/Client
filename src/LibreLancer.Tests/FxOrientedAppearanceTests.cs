using System.Numerics;
using LibreLancer.Fx;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FxOrientedAppearanceTests
{
    [Fact]
    public void AnimatedWidthAndHeightSetIndependentBillboardDimensions()
    {
        var size = FxOrientedAppearance.GetOrientedSize(width: 2f, height: 3f);

        Assert.Equal(new Vector2(4f, 6f), size);
    }
}
