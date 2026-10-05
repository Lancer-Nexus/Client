using LibreLancer.Utf.Ale;
using Xunit;

namespace LibreLancer.Tests;

public class AlchemySParamRangeTests
{
    [Fact]
    public void CurveAnimationClampsBelowFirstSParam()
    {
        var animation = new AlchemyCurveAnimation
        {
            Items =
            [
                new AlchemyCurve { SParam = 0.25f, Value = 10 },
                new AlchemyCurve { SParam = 0.75f, Value = 30 }
            ]
        };

        Assert.Equal(10, animation.GetValue(0.1f, 0));
    }

    [Fact]
    public void FloatAnimationClampsBelowFirstSParam()
    {
        var animation = new AlchemyFloatAnimation
        {
            Items =
            [
                new AlchemyFloats { SParam = 0.25f, Keyframes = [new(0, 10)] },
                new AlchemyFloats { SParam = 0.75f, Keyframes = [new(0, 30)] }
            ]
        };

        Assert.Equal(10, animation.GetValue(0.1f, 0));
    }

    [Fact]
    public void ColorAnimationClampsBelowFirstSParam()
    {
        var first = new Color3f(0.1f, 0.2f, 0.3f);
        var last = new Color3f(0.7f, 0.8f, 0.9f);
        var animation = new AlchemyColorAnimation
        {
            Items =
            [
                new AlchemyColors { SParam = 0.25f, Keyframes = [new(0, first)] },
                new AlchemyColors { SParam = 0.75f, Keyframes = [new(0, last)] }
            ]
        };

        Assert.Equal(first, animation.GetValue(0.1f, 0));
    }
}
