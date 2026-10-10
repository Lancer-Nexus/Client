using LibreLancer.Client;
using Xunit;

namespace LibreLancer.Tests;

public class MouseTargetingGestureTests
{
    [Fact]
    public void QuickClickSelectsWhenReleasedBeforeMouseFlightStarts()
    {
        var gesture = new MouseTargetingGesture();
        gesture.LeftMouseDown(pointerAvailable: true);
        gesture.Update(MouseTargetingGesture.ActivationDelay - 0.01);

        Assert.True(gesture.LeftMouseUp(mouseFlightToggled: false, pointerAvailable: true));
        Assert.False(gesture.IsLeftDown);
        Assert.False(gesture.MouseFlightFromHold);
    }

    [Fact]
    public void ClickReleasedAfterTwoHundredMillisecondsStillSelects()
    {
        var gesture = new MouseTargetingGesture();
        gesture.LeftMouseDown(pointerAvailable: true);
        gesture.Update(0.2);

        Assert.True(gesture.LeftMouseUp(mouseFlightToggled: false, pointerAvailable: true));
    }

    [Fact]
    public void HoldingPastActivationStartsMouseFlightWithoutSelecting()
    {
        var gesture = new MouseTargetingGesture();
        gesture.LeftMouseDown(pointerAvailable: true);
        gesture.Update(MouseTargetingGesture.ActivationDelay + 0.01);

        Assert.True(gesture.MouseFlightFromHold);
        Assert.False(gesture.LeftMouseUp(mouseFlightToggled: false, pointerAvailable: true));
    }

    [Fact]
    public void ManualMouseFlightDoesNotTurnAQuickPressIntoTargetSelection()
    {
        var gesture = new MouseTargetingGesture();
        gesture.LeftMouseDown(pointerAvailable: true);

        Assert.False(gesture.LeftMouseUp(mouseFlightToggled: true, pointerAvailable: true));
    }

    [Fact]
    public void PointerCaptureBlocksSelectionAndCancelsUnavailablePresses()
    {
        var gesture = new MouseTargetingGesture();
        gesture.LeftMouseDown(pointerAvailable: false);

        Assert.False(gesture.IsLeftDown);
        Assert.False(gesture.LeftMouseUp(mouseFlightToggled: false, pointerAvailable: true));

        gesture.LeftMouseDown(pointerAvailable: true);
        Assert.False(gesture.LeftMouseUp(mouseFlightToggled: false, pointerAvailable: false));
    }
}
