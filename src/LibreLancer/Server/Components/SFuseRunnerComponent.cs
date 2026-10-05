// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibreLancer.Data;
using LibreLancer.Data.Schema.Fuses;
using LibreLancer.Data.GameData;
using LibreLancer.Net.Protocol;
using LibreLancer.World;
using LibreLancer.World.Components;

namespace LibreLancer.Server.Components
{
    public class SFuseRunnerComponent : GameComponent
    {
        public List<SpawnedEffect> Effects = [];

        public SFuseRunnerComponent(GameObject parent) : base(parent)
        {
        }

        private class FuseInstance
        {
            public Queue<ScheduledFuseAction> Actions;
            public FuseResources Fuse;
            public double T;

            public FuseInstance(Queue<FuseAction> actions, FuseResources fuse, double t)
            {
                Actions = new(actions.Select(x => new ScheduledFuseAction(
                    x,
                    ResolveActionTime(x.AtT, Random.Shared.NextSingle())))
                    .OrderBy(x => x.AtT));
                Fuse = fuse;
                T = t;
            }
        }

        private readonly record struct ScheduledFuseAction(FuseAction Action, float AtT);

        private List<FuseInstance> instances = [];

        public void Run(FuseResources fuse)
        {
            var instance = new FuseInstance(new(fuse.Fuse.Actions), fuse, 0.0);
            instances.Add(instance);
        }

        private BitArray128 runningHealthFuses = new();
        public List<DamageFuse> DamageFuses = [];

        public bool RunningDeathFuse => instances.Any(x => x.Fuse.Fuse.DeathFuse);

        public void RunAtHealth(float t)
        {
            for (int i = 0; i < DamageFuses.Count; i++)
            {
                if (t < DamageFuses[i].Threshold && !runningHealthFuses[i])
                {
                    runningHealthFuses[i] = true;
                    Run(DamageFuses[i].Fuse!);
                    FLLog.Debug("Server", $"Running fuse {DamageFuses[i].Fuse!.Fuse.Name}");
                }
            }
        }

        private uint fxID = 1;

        public uint AddNetworkEffect(string effect, uint[] hardpoints)
        {
            var id = fxID++;
            Effects.Add(new SpawnedEffect { ID = id, Effect = effect, Hardpoints = hardpoints });
            return id;
        }

        public bool RemoveNetworkEffect(uint id) => Effects.RemoveAll(x => x.ID == id) > 0;

        private void Update(double time, GameWorld world, FuseInstance instance)
        {
            instance.T += time / instance.Fuse.Fuse.Lifetime;
            ScheduledFuseAction scheduled;

            while (instance.Actions.Count > 0 && (scheduled = instance.Actions.Peek()).AtT <= instance.T)
            {
                instance.Actions.Dequeue();
                var act = scheduled.Action;

                if (act is FuseStartEffect fxact)
                {
                    Effects.Add(new SpawnedEffect()
                    {
                        ID = fxID++, Effect = fxact.Effect,
                        Hardpoints = fxact.Hardpoints.Select(FLHash.CreateID).ToArray(),
                    });
                    world.Server!.EffectSpawned(Parent);
                }
                else if (act is FuseDestroyGroup dst)
                {
                    if (dst.Fate == FusePartFate.disappear)
                    {
                        Parent.DisableCmpPart(dst.GroupName!, world, GetResourceManager(world)!, out _);
                    }
                    else if (dst.Fate == FusePartFate.debris)
                    {
                        Parent.SpawnDebris(dst.GroupName!, world, GetResourceManager(world)!);
                    }
                }
                else if (act is FuseDestroyHpAttachment attachment)
                {
                    DestroyHpAttachment(world, attachment);
                }
                else if (act is FuseImpulse impulse)
                {
                    ApplyImpulse(world, impulse);
                }
                else if (act is FuseDamageRoot damageRoot)
                {
                    if (IsAbsoluteDamage(damageRoot.DamageType))
                    {
                        Parent.GetComponent<SHealthComponent>()?.DamageRootFromFuse(damageRoot.Hitpoints);
                    }
                }
                else if (act is FuseDamageGroup damageGroup)
                {
                    ApplyGroupDamage(world, damageGroup);
                }
                else if (act is FuseMakeInvincible makeInvincible)
                {
                    if (Parent.TryGetComponent<SHealthComponent>(out var health))
                    {
                        health.Invulnerable = makeInvincible.TurnOn;
                    }
                }
                else if (act is FuseDumpCargo dumpCargo)
                {
                    DumpCargo(world, dumpCargo);
                }
                else if (act is FuseTumble tumble)
                {
                    ApplyTumble(tumble);
                }
                else if (act is FuseIgniteFuse ig)
                {
                    var start = true;

                    foreach (var fuseInstance in instances)
                    {
                        if (fuseInstance.Fuse.Fuse.Name.Equals(ig.Fuse, StringComparison.OrdinalIgnoreCase))
                        {
                            FLLog.Debug("Fuse", $"Fuse already running {ig.Fuse}");
                            start = false;
                            break;
                        }
                    }

                    if (start)
                    {
                        FLLog.Debug("Fuse", $"Igniting {ig.Fuse}");
                        Run(GetGameData(world)!.Items.Fuses.Get(ig.Fuse)!);
                    }
                }
                else if (act is FuseDestroyRoot)
                {
                    FLLog.Debug("Fuse", $"Killing {Parent}");

                    if (Parent.TryGetComponent<SDestroyableComponent>(out var destroy))
                    {
                        destroy.Destroy(true);
                    }
                }
            }
        }

        internal static float ResolveActionTime(Vector2 range, float randomValue)
        {
            if (range.X < 0)
            {
                return range.Y;
            }

            return range.X + (range.Y - range.X) * MathHelper.Clamp(randomValue, 0, 1);
        }

        internal static float ResolveTumbleThrottle(Vector2 range, float rangeValue, float directionValue)
        {
            var magnitude = ResolveActionTime(range, rangeValue);
            if (magnitude == 0)
            {
                return 0;
            }

            return directionValue < 0.5f ? -magnitude : magnitude;
        }

        private void ApplyTumble(FuseTumble action)
        {
            if (!Parent.TryGetComponent<ShipSteeringComponent>(out var steering) ||
                !Parent.TryGetComponent<ShipPhysicsComponent>(out var physics))
            {
                return;
            }

            steering.TumbleSteering = new Vector3(
                ResolveTumbleThrottle(action.TurnThrottleX, Random.Shared.NextSingle(), Random.Shared.NextSingle()),
                ResolveTumbleThrottle(action.TurnThrottleY, Random.Shared.NextSingle(), Random.Shared.NextSingle()),
                ResolveTumbleThrottle(action.TurnThrottleZ, Random.Shared.NextSingle(), Random.Shared.NextSingle()));
            steering.TumbleThrottle = ResolveActionTime(action.Throttle, Random.Shared.NextSingle());
            physics.AngularDragScale = action.AngularDragScale > 0 ? action.AngularDragScale : 1;
        }

        private static bool IsAbsoluteDamage(string? damageType)
        {
            if (string.Equals(damageType, "absolute", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            FLLog.Warning("Fuse", $"Unsupported fuse damage_type '{damageType ?? "(missing)"}', expected 'absolute'.");
            return false;
        }

        private void ApplyGroupDamage(GameWorld world, FuseDamageGroup action)
        {
            if (!IsAbsoluteDamage(action.DamageType) || action.Hitpoints <= 0 ||
                string.IsNullOrWhiteSpace(action.GroupName) || Parent.Model?.RigidModel.Parts == null ||
                !Parent.Model.RigidModel.Parts.TryGetPart(action.GroupName, out var part) ||
                !Parent.TryGetComponent<SHealthComponent>(out var health))
            {
                return;
            }

            var destroyedPart = health.Damage(action.Hitpoints, 0, null, part);
            if (destroyedPart == null || !Parent.Model.TryGetCollisionGroup(destroyedPart, out var collisionGroup))
            {
                return;
            }

            var resources = GetResourceManager(world)!;
            if (collisionGroup.Definition.Separable)
            {
                Parent.SpawnDebris(destroyedPart.Name!, world, resources);
            }
            else
            {
                Parent.DisableCmpPart(destroyedPart.Name!, world, resources, out _);
            }
        }

        private void DestroyHpAttachment(GameWorld world, FuseDestroyHpAttachment action)
        {
            var hardpoints = Parent.Children
                .Where(x => x.Attachment != null && x.TryGetComponent<EquipmentComponent>(out _))
                .Select(x => x.Attachment!.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (hardpoints.Length == 0 || string.IsNullOrWhiteSpace(action.Hardpoint))
            {
                return;
            }

            var hardpointName = action.Hardpoint.Equals("random", StringComparison.OrdinalIgnoreCase)
                ? SelectRandomHardpoint(hardpoints, Random.Shared.NextSingle())
                : hardpoints.FirstOrDefault(x => x.Equals(action.Hardpoint, StringComparison.OrdinalIgnoreCase));
            if (hardpointName == null || Parent.GetHardpoint(hardpointName) is not { } hardpoint)
            {
                return;
            }

            var child = Parent.Children.FirstOrDefault(x =>
                x.Attachment?.Name.Equals(hardpointName, StringComparison.OrdinalIgnoreCase) == true &&
                x.TryGetComponent<EquipmentComponent>(out _));
            if (child == null)
            {
                return;
            }

            if (action.Fate == FusePartFate.debris &&
                child.TryGetComponent<CargoPodComponent>(out _) &&
                child.TryGetComponent<SHealthComponent>(out var health))
            {
                health.DamageRootFromFuse(health.CurrentHealth);
                return;
            }

            if (action.Fate == FusePartFate.loot &&
                child.TryGetComponent<EquipmentComponent>(out var equipment) &&
                equipment.Equipment.LootAppearance is { } lootAppearance)
            {
                var transform = hardpoint.Transform * Parent.WorldTransform;
                var direction = Random.Shared.NextUnitVector();
                var impulse = direction * (40 + (Random.Shared.NextSingle() * 60));
                world.Server?.SpawnLoot(lootAppearance, equipment.Equipment, 1, transform,
                    initialImpulse: impulse);
            }

            if (Parent.RemoveEquipment(hardpointName, world))
            {
                world.Server?.EquipmentDestroyed(Parent, hardpoint, action.Fate == FusePartFate.debris);
            }
        }

        private void DumpCargo(GameWorld world, FuseDumpCargo action)
        {
            if (world.Server == null)
            {
                return;
            }

            var origin = Parent.WorldTransform;
            if (!string.IsNullOrWhiteSpace(action.OriginHardpoint) &&
                Parent.GetHardpoint(action.OriginHardpoint) is { } hardpoint)
            {
                origin = hardpoint.Transform * origin;
            }

            var cargo = new List<BasicCargo>();
            if (Parent.TryGetComponent<SPlayerComponent>(out var playerCargo))
            {
                cargo.AddRange(playerCargo.TakeLootableCargoForFuse());
            }
            if (Parent.TryGetComponent<SNPCCargoComponent>(out var npcCargo))
            {
                cargo.AddRange(npcCargo.TakeLootableCargoForFuse());
            }

            foreach (var child in Parent.Children)
            {
                if (child.TryGetComponent<CargoPodComponent>(out var cargoPod))
                {
                    cargo.AddRange(cargoPod.TakeLootableCargoForFuse());
                }
            }

            foreach (var item in cargo)
            {
                var remaining = item.Count;
                while (remaining > 0)
                {
                    var count = Math.Min(30, remaining);
                    remaining -= count;
                    var direction = Random.Shared.NextUnitVector();
                    var offset = direction * (2 + Random.Shared.NextSingle() * 4);
                    var impulse = direction * (40 + Random.Shared.NextSingle() * 60);
                    world.Server.SpawnLoot(item.Item.LootAppearance!, item.Item, count,
                        new Transform3D(origin.Position + offset, origin.Orientation),
                        initialImpulse: impulse);
                }
            }
        }

        internal static string? SelectRandomHardpoint(IReadOnlyList<string> hardpoints, float randomValue)
        {
            if (hardpoints.Count == 0)
            {
                return null;
            }

            var normalized = MathHelper.Clamp(randomValue, 0, 1);
            var index = Math.Min(hardpoints.Count - 1, (int)(normalized * hardpoints.Count));
            return hardpoints[index];
        }

        private void ApplyImpulse(GameWorld world, FuseImpulse action)
        {
            var physics = world.Physics;
            if (physics == null || action.Radius <= 0)
            {
                return;
            }

            var source = Parent.WorldTransform;
            if (!string.IsNullOrWhiteSpace(action.Hardpoint) &&
                Parent.GetHardpoint(action.Hardpoint) is { } hardpoint)
            {
                source = hardpoint.Transform * source;
            }

            var origin = source.Transform(action.PosOffset);
            var affected = new HashSet<GameObject>();
            foreach (var hit in physics.SphereTest(origin, action.Radius))
            {
                if (hit?.Tag is not GameObject target ||
                    ReferenceEquals(target, Parent) ||
                    !affected.Add(target))
                {
                    continue;
                }

                var targetPhysics = target.PhysicsComponent;
                if (targetPhysics is null || targetPhysics.Mass <= 0)
                {
                    continue;
                }

                var impulse = CalculateImpulse(origin, targetPhysics.Body.Position, action.Radius, action.Force);
                if (impulse != Vector3.Zero)
                {
                    targetPhysics.Body.Impulse(impulse);
                }

                if (action.Damage > 0 && target.TryGetComponent<SHealthComponent>(out var health))
                {
                    health.DamageExplosion(action.Damage, 0, Parent, origin, action.Radius);
                }
            }
        }

        internal static Vector3 CalculateImpulse(Vector3 origin, Vector3 target, float radius, float force)
        {
            var offset = target - origin;
            var distance = offset.Length();
            if (radius <= 0 || distance <= 0 || distance >= radius || force == 0)
            {
                return Vector3.Zero;
            }

            return offset / distance * (force * (1f - distance / radius));
        }

        public override void Update(double time, GameWorld world)

        {
            for (int i = 0; i < instances.Count; i++)
            {
                Update(time, world, instances[i]);
            }
        }
    }
}
