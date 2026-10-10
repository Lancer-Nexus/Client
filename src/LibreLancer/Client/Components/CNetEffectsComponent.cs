// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using LibreLancer.Net.Protocol;
using LibreLancer.Render;
using LibreLancer.Resources;
using LibreLancer.World;

namespace LibreLancer.Client.Components
{
    public class CNetEffectsComponent : GameComponent
    {
        private SpawnedEffect[] effects = [];

        public CNetEffectsComponent(GameObject parent) : base(parent)
        {
        }

        private int renIndex = 0;

        private Dictionary<uint, List<ParticleEffectRenderer>> spawned = [];

        private void Spawn(SpawnedEffect effect, GameWorld world)
        {
            var fx = GetGameData(world).Items?.Effects?.Get(effect.Effect);
            if (fx is null)
            {
                spawned[effect.ID] = [];
                return;
            }

            if (fx.Sound is not null && world.Sounds is not null)
            {
                var sound = world.Sounds.GetInstance(fx.Sound.Nickname, 0, -1, -1,
                    Parent?.WorldTransform.Position);
                sound?.Play();
            }

            var pfx = fx.GetEffect(world.Renderer?.ResourceManager!);
            if (pfx is null)
            {
                spawned[effect.ID] = [];
                return;
            }

            if (effect.Hardpoints.Length == 0)
            {
                var fxobj = new ParticleEffectRenderer(pfx) { Index = renIndex++ };
                Parent?.ExtraRenderers.Add(fxobj);
                spawned.Add(effect.ID, [fxobj]);
                return;
            }

            var attachedRenderers = new List<ParticleEffectRenderer>();
            foreach (var fxhp in effect.Hardpoints)
            {
                var hp = Parent?.GetHardpoint(fxhp);

                if (hp is null)
                {
                    continue;
                }

                var fxobj = new ParticleEffectRenderer(pfx) {Index = renIndex++, Attachment = hp};
                Parent?.ExtraRenderers.Add(fxobj);
                attachedRenderers.Add(fxobj);
            }
            spawned[effect.ID] = attachedRenderers;
        }

        public void UpdateEffects(SpawnedEffect[] fx, GameWorld world)
        {
            foreach (var f in fx)
            {
                if (!spawned.ContainsKey(f.ID)) Spawn(f, world);
            }

            foreach (var old in effects)
            {
                if (Array.Exists(fx, current => current.ID == old.ID))
                    continue;
                if (!spawned.Remove(old.ID, out var renderers))
                    continue;
                foreach (var renderer in renderers)
                    Parent?.ExtraRenderers.Remove(renderer);
            }

            effects = fx;
        }
    }
}
