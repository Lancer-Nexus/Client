using System;
using System.Numerics;

namespace LibreLancer.Server.Components;

/// <summary>A small deterministic random stream whose state can be transferred with an NPC.</summary>
public sealed class SnapshotableRandom
{
    private ulong state;
    public ulong State
    {
        get => state;
        set => state = value == 0
            ? throw new ArgumentOutOfRangeException(nameof(value), "Random state must be non-zero.")
            : value;
    }

    public SnapshotableRandom()
    {
        State = unchecked((ulong)Random.Shared.NextInt64());
        if (State == 0)
            State = 0x9E3779B97F4A7C15UL;
    }

    private ulong NextUInt64()
    {
        var value = State;
        value ^= value >> 12;
        value ^= value << 25;
        value ^= value >> 27;
        State = value;
        return value * 0x2545F4914F6CDD1DUL;
    }

    public int Next(int maxValue)
    {
        if (maxValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue));
        return (int)(NextUInt64() % (uint)maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentOutOfRangeException(nameof(maxValue));
        return minValue + Next(maxValue - minValue);
    }

    public float NextSingle() => (NextUInt64() >> 40) * (1f / 16777216f);

    public float NextFloat(float minValue, float maxValue) =>
        minValue + NextSingle() * (maxValue - minValue);

    private static uint NextLcg24(ref uint seed)
    {
        var value = seed * 747796405U + 2891336453U;
        value ^= value >> 14;
        seed = value;
        return value >> 8;
    }

    public Vector3 NextUnitVector()
    {
        var seed = (uint)Next(int.MaxValue);
        const float scale = 2f / 16777216f;
        var vector = new Vector3(
            NextLcg24(ref seed) * scale - 1,
            NextLcg24(ref seed) * scale - 1,
            NextLcg24(ref seed) * scale - 1);
        return Vector3.Normalize(vector);
    }
}
