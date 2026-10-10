// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using System.Numerics;

using System.Linq;

namespace LibreLancer.Data.Schema;

public class FloatGraph
{
    public string? Name;
    public List<Vector2> Points = [];
}

public readonly record struct ColorGraphPoint(float Time, uint Color);

public class ColorGraph
{
    public string? Name;
    public List<ColorGraphPoint> Points = [];

    public uint EvaluatePackedColor(double time, float period)
    {
        if (Points.Count == 0)
            return uint.MaxValue;
        if (Points.Count == 1)
            return Points[0].Color;

        var minTime = Points.Min(p => p.Time);
        var maxTime = Points.Max(p => p.Time);
        var range = maxTime - minTime;
        if (!float.IsFinite(range) || range <= float.Epsilon)
            return Points[^1].Color;

        float normalized;
        if (float.IsFinite(period) && period > 0)
        {
            var cycle = time % period;
            if (cycle < 0)
                cycle += period;
            normalized = (float)(cycle / period);
        }
        else
        {
            normalized = (float)Math.Clamp(time, 0, 1);
        }

        var sampleTime = minTime + range * normalized;
        var upper = UpperBound(sampleTime);
        if (upper <= 0)
            return Points[0].Color;
        if (upper >= Points.Count)
            return Points[^1].Color;

        var left = Points[upper - 1];
        var right = Points[upper];
        var span = right.Time - left.Time;
        if (!float.IsFinite(span) || span <= float.Epsilon)
            return right.Color;

        var amount = Math.Clamp((sampleTime - left.Time) / span, 0, 1);
        return LerpColor(left.Color, right.Color, amount);
    }

    private int UpperBound(float time)
    {
        var low = 0;
        var high = Points.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (Points[middle].Time <= time)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private static uint LerpColor(uint left, uint right, float amount)
    {
        static uint Channel(uint a, uint b, int shift, float t)
        {
            var first = (a >> shift) & 0xff;
            var second = (b >> shift) & 0xff;
            return (uint)Math.Clamp((int)MathF.Round(first + ((int)second - (int)first) * t), 0, 255);
        }

        return (Channel(left, right, 24, amount) << 24) |
               (Channel(left, right, 16, amount) << 16) |
               (Channel(left, right, 8, amount) << 8) |
               Channel(left, right, 0, amount);
    }
}
