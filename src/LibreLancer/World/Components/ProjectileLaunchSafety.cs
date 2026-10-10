namespace LibreLancer.World.Components;

public static class ProjectileLaunchSafety
{
    public const double CollisionGracePeriod = 1.0;

    public static bool IsCollisionSafe(double elapsed) => elapsed < CollisionGracePeriod;
}
