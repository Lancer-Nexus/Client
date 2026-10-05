// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using LibreLancer.Render;
using LibreLancer.World.Components;
using LibreLancer.World;

namespace LibreLancer.Client.Components
{
    public class CUpdateSParamComponent : GameComponent
    {
        public CUpdateSParamComponent(GameObject parent) : base(parent)
        {
        }

        internal static float CalculateSParam(bool engineKill, float engineSpeed, float velocity, float maxVelocity)
        {
            if (!engineKill)
                return engineSpeed;
            if (maxVelocity <= 0)
                return 0;
            return Math.Clamp(velocity / maxVelocity, 0, 1);
        }

        public override void Update(double time, GameWorld world)
        {
            if (Parent?.RenderComponent is not ParticleEffectRenderer renderer)
                return;

            var ship = Parent.Parent;
            var engine = ship?.GetComponent<CEngineComponent>();
            if (engine == null)
                return;

            var sparam = engine.Speed;
            if (engine.EngineKill && ship?.GetComponent<ShipPhysicsComponent>() is { } physics)
            {
                var velocity = ship.PhysicsComponent?.Body.LinearVelocity.Length() ?? 0;
                var totalDrag = physics.Ship.LinearDrag + engine.Engine.Def.LinearDrag;
                var maxVelocity = totalDrag > 0
                    ? engine.Engine.Def.MaxForce / totalDrag
                    : 0;
                sparam = CalculateSParam(true, engine.Speed, velocity, maxVelocity);
            }

            renderer.SParam = sparam;
        }
    }
}
