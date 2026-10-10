using LibreLancer.Utf.Ale;
using Xunit;

namespace LibreLancer.Tests;

public class AlchemyEmptyAnimationTests
{
    [Fact]
    public void FloatCurveWithNoKeyframesReturnsZero()
    {
        var curve = new AlchemyFloats { Keyframes = [] };

        Assert.Equal(0, curve.GetValue(0.5f));
    }

    [Fact]
    public void FloatAnimationWithNoItemsReturnsZero()
    {
        var animation = new AlchemyFloatAnimation { Items = [] };

        Assert.Equal(0, animation.GetValue(0.5f, 0.5f));
    }

    [Fact]
    public void FloatAnimationWithAnEmptyKeyframeListReturnsZero()
    {
        var animation = new AlchemyFloatAnimation
        {
            Items = [new AlchemyFloats { Keyframes = [] }]
        };

        Assert.Equal(0, animation.GetValue(0.5f, 0.5f));
    }
}
