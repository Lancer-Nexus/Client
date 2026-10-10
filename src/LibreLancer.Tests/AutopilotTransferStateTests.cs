using System;
using System.Numerics;
using System.Text.Json;
using LibreLancer.World;
using LibreLancer.World.Components;
using Xunit;

namespace LibreLancer.Tests;

public sealed class AutopilotTransferStateTests
{
    [Fact]
    public void GotoVectorSnapshot_ValidatesAndPreservesTargetAndThrottle()
    {
        var state = new AutopilotTransferState
        {
            Behavior = AutopilotBehaviors.Goto,
            CanCruise = true,
            HasTriggeredCruise = true,
            MaxThrottle = 0.75f,
            GotoRadius = 25,
            TargetPosition = new Vector3(10, 20, 30),
            TargetRadius = 5,
            PitchController = new PIDControllerTransferState(0, 0, 0, 0, 0),
            YawController = new PIDControllerTransferState(0, 0, 0, 0, 0),
            Avoidance = new AutopilotAvoidanceTransferState()
        };

        state.Validate();

        Assert.Equal(new Vector3(10, 20, 30), state.TargetPosition);
        Assert.Equal(0.75f, state.MaxThrottle);
        Assert.True(state.HasTriggeredCruise);
    }

    [Fact]
    public void SnapshotValidation_RejectsNonFiniteAndUnsupportedBehavior()
    {
        Assert.Throws<ArgumentException>(() => new AutopilotTransferState
            { Behavior = AutopilotBehaviors.Dock }.Validate());
        Assert.Throws<ArgumentException>(() => new AutopilotTransferState
            { Behavior = AutopilotBehaviors.Goto, TargetPosition = new Vector3(float.NaN, 0, 0) }.Validate());
    }

    [Fact]
    public void AvoidanceVectorJson_RoundTripsDirectionAndState()
    {
        var options = new JsonSerializerOptions();
        var source = new AutopilotAvoidanceTransferState
        {
            Strafe = StrafeControls.Right | StrafeControls.Up,
            Vector = Vector2.Normalize(new Vector2(0.6f, 0.8f)),
            ClearTimer = 0.4f
        };

        var json = JsonSerializer.Serialize(source, options);
        var restored = JsonSerializer.Deserialize<AutopilotAvoidanceTransferState>(json, options)!;

        restored.Validate();
        Assert.Equal(source.Strafe, restored.Strafe);
        Assert.Equal(source.Vector, restored.Vector);
        Assert.Equal(source.ClearTimer, restored.ClearTimer);
    }
}
