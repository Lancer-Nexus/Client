// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Diagnostics;
using System.Numerics;
using LibreLancer.Utf.Ale;

namespace LibreLancer.Fx
{
    public abstract class FxEmitter : FxNode
    {
        public int InitialParticles;
        public AlchemyCurveAnimation? Frequency;
        public AlchemyCurveAnimation? EmitCount;

        public AlchemyCurveAnimation InitLifeSpan = new (1);

        // public AlchemyCurveAnimation LODCurve; -- Not really relevant in a modern context
        public AlchemyCurveAnimation Pressure = new(0);
        public AlchemyCurveAnimation? VelocityApproach;
        public AlchemyCurveAnimation? MaxParticles;

        protected FxEmitter(AlchemyNode ale) : base(ale)
        {
            if (ale.TryGetParameter(AleProperty.Emitter_InitialParticles, out var temp))
            {
                InitialParticles = (int) (uint) temp.Value;
            }

            if (ale.TryGetParameter(AleProperty.Emitter_Frequency, out temp))
            {
                Frequency = (AlchemyCurveAnimation) temp.Value;
            }

            if (ale.TryGetParameter(AleProperty.Emitter_EmitCount, out temp))
            {
                EmitCount = (AlchemyCurveAnimation) temp.Value;
            }

            if (ale.TryGetParameter(AleProperty.Emitter_InitLifeSpan, out temp))
            {
                InitLifeSpan = (AlchemyCurveAnimation) temp.Value;
            }

            if (ale.TryGetParameter(AleProperty.Emitter_Pressure, out temp))
            {
                Pressure = (AlchemyCurveAnimation) temp.Value;
            }

            if (ale.TryGetParameter(AleProperty.Emitter_VelocityApproach, out temp))
            {
                VelocityApproach = (AlchemyCurveAnimation) temp.Value;
            }

            if (ale.TryGetParameter(AleProperty.Emitter_MaxParticles, out temp))
            {
                MaxParticles = (AlchemyCurveAnimation) temp.Value;
            }
        }

        protected FxEmitter(string name) : base(name) { }

        public override AlchemyNode SerializeNode()
        {
            var n = base.SerializeNode();

            n.Parameters.Add(new(AleProperty.Emitter_InitialParticles, (uint) InitialParticles));

            if (Frequency != null)
            {
                n.Parameters.Add(new(AleProperty.Emitter_Frequency, Frequency));
            }

            if (EmitCount != null)
            {
                n.Parameters.Add(new(AleProperty.Emitter_EmitCount, EmitCount));
            }

            n.Parameters.Add(new(AleProperty.Emitter_InitLifeSpan, InitLifeSpan));
            n.Parameters.Add(new(AleProperty.Emitter_Pressure, Pressure));

            if (VelocityApproach != null)
            {
                n.Parameters.Add(new(AleProperty.Emitter_VelocityApproach, VelocityApproach));
            }

            if (MaxParticles != null)
            {
                n.Parameters.Add(new(AleProperty.Emitter_MaxParticles, MaxParticles));
            }

            return n;
        }

        protected virtual void SetParticle(ParticleEffectInstance instance, EmitterReference reference, ref Particle particle, float sparam,
            float globaltime)
        {
        }

        private static readonly AlchemyTransform[] _transforms = new AlchemyTransform[32];

        protected bool DoTransform(NodeReference reference, float sparam, float t, out Vector3 translate,
            out Quaternion rotate)
        {
            translate = Vector3.Zero;
            rotate = Quaternion.Identity;

            var idx = -1;
            var pr = reference;

            while (pr != null && !pr.IsAttachmentNode)
            {
                if (pr.Node.Transform.HasTransform)
                {
                    idx++;
                    _transforms[idx] = pr.Node.Transform;
                }

                pr = pr.Parent;
            }

            for (var i = idx; i >= 0; i--)
            {
                translate += _transforms[i].GetTranslation(sparam, t);
                rotate *= _transforms[i].GetRotation(sparam, t);
            }

            return idx != -1;
        }

        private static float Max3(float a, float b, float c) => Math.Max(Math.Max(a, b), c);

        public float GetMaxDistance(NodeReference reference)
        {
            var pr = reference;

            float max = 0;

            while (pr != null && !pr.IsAttachmentNode)
            {
                if (pr.Node.Transform.HasTransform)
                {
                    max += Max3(pr.Node.Transform.TranslateX.GetMax(true), pr.Node.Transform.TranslateY.GetMax(true),
                        pr.Node.Transform.TranslateZ.GetMax(true));
                }

                pr = pr.Parent;
            }

            return max;
        }

        public void Update(EmitterReference reference, int index, ParticleEffectInstance instance, double delta,
            ref Matrix4x4 transform, float sparam)
        {
            if (reference.Linked == null)
            {
                return;
            }

            if (NodeLifeSpan < instance.GlobalTime)
            {
                return;
            }

            var maxCount = MaxParticles == null
                ? int.MaxValue
                : (int) Math.Ceiling(MaxParticles.GetValue(sparam, (float) instance.GlobalTime));
            var freq = Frequency?.GetValue(sparam, (float) instance.GlobalTime) ?? 0f;
            var lifespan = InitLifeSpan.GetValue(sparam, 0f);

            if (lifespan <= 0)
            {
                return;
            }

            ref EmitterState state = ref instance.Emitters[index];

            if (!state.Initialized)
            {
                state.Initialized = true;
                var initialCount = Math.Min(InitialParticles, maxCount);
                var initialWindow = freq > 0 ? Math.Min(lifespan, initialCount / freq) : 0;
                var initialStep = initialCount > 0 ? initialWindow / initialCount : 0;
                for (var i = 0; i < initialCount; i++)
                    EmitParticle(reference, index, instance, sparam, lifespan, i * initialStep, ref state);
            }

            if(freq > 0)
            {
                // Spawn lots of particles
                var dt = Math.Min(delta, 3); // don't go crazy during debug pauses

                //Number of particles to spawn in this timestep
                int toEmit = 0;

                //Calculate amount of particles to spawn as float value
                state.NextEmitCount += dt*freq;

                if (state.NextEmitCount > 1)
                {
                    //Use floored value to determine integral amount of particles to spawn for this time step
                    toEmit = Math.Min((int) Math.Floor(state.NextEmitCount), maxCount);
                    //Subtract the amount of particles which will be spawned
                    state.NextEmitCount -= toEmit;
                }

                //In case of spawning more than one particle, set starting time how long they are alive evenly distributed to simulate non discrete behaviour
                double correctionFactor = 0;
                if(toEmit > 1)
                    correctionFactor =  dt / toEmit;

                //Spawn particles for this time step
                for (int count=0; count < toEmit; count++)
                {

                    if (lifespan < dt) // Don't spawn if it is already gone
                    {
                        continue;
                    }

                    if (state.Count >= maxCount)
                    {
                        continue;
                    }

                    EmitParticle(reference, index, instance, sparam, lifespan,
                        count * correctionFactor, ref state);
                }
            }
            else
            {
                state.NextEmitCount = 0;
            }
        }

        private void EmitParticle(EmitterReference reference, int index, ParticleEffectInstance instance,
            float sparam, float lifespan, double timeAlive, ref EmitterState state)
        {
            ref var particle = ref instance.Buffer.Enqueue(reference.AppBufIdx, out var despawned);
            if (despawned != -1)
            {
                instance.Emitters[despawned].Count--;
                Debug.Assert(instance.Emitters[despawned].Count >= 0);
            }

            particle.LifeSpan = lifespan;
            particle.TimeAlive = (float)timeAlive;
            particle.EmitterIndex = index;
            particle.Id = instance.AllocateParticleId();
            particle.Orientation = Quaternion.Identity;
            SetParticle(instance, reference, ref particle, sparam, (float)instance.GlobalTime);
            if (particle.Normal.LengthSquared() <= float.Epsilon)
                particle.Normal = particle.Velocity.LengthSquared() > float.Epsilon
                    ? particle.Velocity.Normalized()
                    : Vector3.UnitY;
            if (timeAlive > 0)
                particle.Position += particle.Velocity * (float)timeAlive;
            state.Count++;
        }
    }
}
