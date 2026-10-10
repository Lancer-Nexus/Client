using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using LibreLancer.Thn;
using LibreLancer.Thn.Events;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ThnSpatialPropAnimTests
{
    [Fact]
    public void StaticSpatialPositionUsesParameterCurve()
    {
        var ship = new ThnSceneObject();
        var instance = CreateInstance();
        instance.Objects["ship"] = ship;

        new StartSpatialPropAnimEvent
        {
            Duration = 10,
            Targets = ["ship"],
            SetFlags = StartSpatialPropAnimEvent.AnimVars.Pos,
            Pos = new Vector3(10, 0, 0),
            ParamCurve = SmoothCurve()
        }.Run(instance);

        instance.Update(2.5);

        Assert.Equal(new Vector3(1.5625f, 0, 0), ship.Translate);
    }

    [Fact]
    public void FollowSpatialPositionUsesParameterCurve()
    {
        var ship = new ThnSceneObject();
        var marker = new ThnSceneObject { Translate = new Vector3(10, 0, 0) };
        var instance = CreateInstance();
        instance.Objects["ship"] = ship;
        instance.Objects["marker"] = marker;

        new StartSpatialPropAnimEvent
        {
            Duration = 10,
            Targets = ["ship", "marker"],
            SetFlags = StartSpatialPropAnimEvent.AnimVars.Pos,
            ParamCurve = SmoothCurve()
        }.Run(instance);

        instance.Update(2.5);

        Assert.Equal(new Vector3(1.5625f, 0, 0), ship.Translate);
    }

    [Fact]
    public void StaticSpatialOrientationUsesParameterCurve()
    {
        var ship = new ThnSceneObject { Rotate = Quaternion.Identity };
        var instance = CreateInstance();
        instance.Objects["ship"] = ship;
        var target = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        new StartSpatialPropAnimEvent
        {
            Duration = 10,
            Targets = ["ship"],
            SetFlags = StartSpatialPropAnimEvent.AnimVars.QOrient,
            Q_Orient = target,
            ParamCurve = SmoothCurve()
        }.Run(instance);

        instance.Update(2.5);

        var expected = Quaternion.Slerp(Quaternion.Identity, target, 1.5625f / 10f);
        Assert.True(MathF.Abs(Quaternion.Dot(expected, ship.Rotate)) > 0.99999f,
            $"Expected {expected}, got {ship.Rotate}");
    }

    private static ParameterCurve SmoothCurve() => new(
        PCurveType.Smooth,
        [new Vector4(0, 0, 0, 0), new Vector4(1, 1, 0, 0)]);

    private static ThnScriptInstance CreateInstance() => new(
        (Cutscene)RuntimeHelpers.GetUninitializedObject(typeof(Cutscene)),
        new ThnScript { Duration = 20, Events = [] });
}
