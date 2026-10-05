using LibreLancer.Data.GameData.Items;
using LibreLancer.World;
using LibreLancer.World.Components;

namespace LibreLancer.Client.Components;

public class CDeployedMunitionComponent : GameComponent
{
    public Equipment Equipment { get; }
    public MunitionEquip? Munition => Equipment as MunitionEquip;
    private double elapsed;

    public CDeployedMunitionComponent(GameObject parent, Equipment equipment) : base(parent)
    {
        Equipment = equipment;
    }

    public override void Register(GameWorld world)
    {
        if (Equipment is not (MunitionEquip or MissileEquip))
        {
            return;
        }

        if (Parent.PhysicsComponent is { } physics)
        {
            physics.Collidable = false;
            if (physics.Body != null)
            {
                physics.Body.Collidable = false;
            }
        }
    }

    public override void Update(double time, GameWorld world)
    {
        if (Equipment is not (MunitionEquip or MissileEquip) ||
            Parent.PhysicsComponent is not { } physics || physics.Body == null)
        {
            return;
        }

        elapsed += time;
        var collidable = !ProjectileLaunchSafety.IsCollisionSafe(elapsed);
        physics.Collidable = collidable;
        physics.Body.Collidable = collidable;
    }
}

public class CMissileComponent : CDeployedMunitionComponent
{
    public MissileEquip Missile;
    public CMissileComponent(GameObject parent, MissileEquip missile) : base(parent, missile)
    {
        this.Missile = missile;
    }
}
