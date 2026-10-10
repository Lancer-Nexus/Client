using LibreLancer.World.Components;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ProjectileLaunchSafetyTests
{
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.999, true)]
    [InlineData(1.0, false)]
    [InlineData(1.001, false)]
    public void CollisionGracePeriodIsOneSecond(double elapsed, bool expected)
    {
        Assert.Equal(expected, ProjectileLaunchSafety.IsCollisionSafe(elapsed));
    }
}
