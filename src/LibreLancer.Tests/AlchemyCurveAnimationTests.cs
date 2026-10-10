using LibreLancer.Utf.Ale;
using Xunit;

namespace LibreLancer.Tests;

public class AlchemyCurveAnimationTests
{
    [Fact]
    public void EmptyAnimationReturnsZero()
    {
        var animation = new AlchemyCurveAnimation { Items = [] };

        Assert.Equal(0, animation.GetValue(0.5f, 0.5f));
    }

    [Fact]
    public void RepeatUsesTheSpanBetweenFirstAndLastKeyframes()
    {
        var curve = new AlchemyCurve
        {
            Flags = LoopFlags.Repeat,
            IsCurve = true,
            Keyframes =
            [
                new CurveKeyframe { Time = 2, Value = 10 },
                new CurveKeyframe { Time = 4, Value = 30 }
            ]
        };

        Assert.Equal(20, curve.GetValue(5));
        Assert.Equal(10, curve.GetValue(6));
    }

    [Fact]
    public void RepeatWithZeroDurationReturnsTheFinalKeyframeValue()
    {
        var curve = new AlchemyCurve
        {
            Flags = LoopFlags.Repeat,
            IsCurve = true,
            Keyframes =
            [
                new CurveKeyframe { Time = 3, Value = 10 },
                new CurveKeyframe { Time = 3, Value = 20 }
            ]
        };

        Assert.Equal(20, curve.GetValue(4));
    }
}
