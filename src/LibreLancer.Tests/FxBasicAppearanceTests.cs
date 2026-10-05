using System.Numerics;
using LibreLancer.Fx;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FxBasicAppearanceTests
{
    [Fact]
    public void HorizontalToVerticalAspectScalesOnlyBillboardWidth()
    {
        var size = FxBasicAppearance.GetAspectAdjustedSize(2f, 1.5f);

        Assert.Equal(new Vector2(6f, 4f), size);
    }

    [Fact]
    public void UnitAspectKeepsSquareBillboard()
    {
        var size = FxBasicAppearance.GetAspectAdjustedSize(2f, 1f);

        Assert.Equal(new Vector2(4f, 4f), size);
    }
}
