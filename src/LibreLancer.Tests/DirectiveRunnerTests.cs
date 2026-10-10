using LibreLancer.Missions.Directives;
using LibreLancer.World;
using LibreLancer.World.Components;
using System.Numerics;
using Xunit;

namespace LibreLancer.Tests;

public class DirectiveRunnerTests
{
    [Fact]
    public void CruiseContinuesBetweenGotoDirectives()
    {
        Assert.False(DirectiveRunnerComponent.ShouldStopAtTarget(
            GotoKind.GotoCruise, new GotoVecDirective()));
    }

    [Fact]
    public void CruiseStopsBeforeDelayOrEndOfList()
    {
        Assert.True(DirectiveRunnerComponent.ShouldStopAtTarget(
            GotoKind.GotoCruise, new DelayDirective()));
        Assert.True(DirectiveRunnerComponent.ShouldStopAtTarget(
            GotoKind.GotoCruise, null));
    }

    [Fact]
    public void NoCruiseGotoAlwaysStopsAtTarget()
    {
        Assert.True(DirectiveRunnerComponent.ShouldStopAtTarget(
            GotoKind.GotoNoCruise, new GotoVecDirective()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AvoidanceDirectiveUpdatesAutopilot(bool enabled)
    {
        var ship = new GameObject();
        var autopilot = new AutopilotComponent(ship);
        ship.AddComponent(autopilot);
        var runner = new DirectiveRunnerComponent(ship);

        runner.SetDirectives([new AvoidanceDirective { Avoidance = enabled }], null!);

        Assert.Equal(enabled, autopilot.AvoidanceEnabled);
    }

    [Fact]
    public void GotoSplineUsesMissionRangeForEveryWaypoint()
    {
        var ship = new GameObject();
        var autopilot = new AutopilotComponent(ship);
        ship.AddComponent(autopilot);
        var runner = new DirectiveRunnerComponent(ship);
        var spline = new GotoSplineDirective
        {
            PointA = Vector3.Zero,
            PointB = new Vector3(100, 0, 0),
            PointC = new Vector3(200, 0, 0),
            PointD = new Vector3(300, 0, 0),
            CruiseKind = GotoKind.GotoNoCruise,
            Range = 500,
            MaxThrottle = 30
        };

        runner.SetDirectives([spline], null!);
        Assert.Equal(500, autopilot.CaptureTransferState().GotoRadius);

        autopilot.Cancel();
        runner.Update(0, null!);

        Assert.Equal(500, autopilot.CaptureTransferState().GotoRadius);
    }
}
