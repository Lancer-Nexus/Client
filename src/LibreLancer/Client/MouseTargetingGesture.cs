namespace LibreLancer.Client;

internal sealed class MouseTargetingGesture
{
    // Issue #410 describes vanilla mouse flight activating after around 500 ms.
    internal const double ActivationDelay = 0.5;

    public bool IsLeftDown { get; private set; }
    public double TimeUntilMouseFlight { get; private set; }
    public bool MouseFlightFromHold => IsLeftDown && TimeUntilMouseFlight < 0;

    public void LeftMouseDown(bool pointerAvailable)
    {
        if (!pointerAvailable)
        {
            Cancel();
            return;
        }

        if (!IsLeftDown)
        {
            IsLeftDown = true;
            TimeUntilMouseFlight = ActivationDelay;
        }
    }

    public void Update(double delta)
    {
        if (IsLeftDown)
        {
            TimeUntilMouseFlight -= delta;
        }
    }

    public bool LeftMouseUp(bool mouseFlightToggled, bool pointerAvailable)
    {
        var select = IsLeftDown && TimeUntilMouseFlight >= 0 && !mouseFlightToggled && pointerAvailable;
        Cancel();
        return select;
    }

    public void Cancel()
    {
        IsLeftDown = false;
        TimeUntilMouseFlight = 0;
    }
}
