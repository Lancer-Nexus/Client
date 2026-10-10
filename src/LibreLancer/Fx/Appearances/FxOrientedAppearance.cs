// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Numerics;
using LibreLancer.Render.Materials;
using LibreLancer.Utf.Ale;

namespace LibreLancer.Fx
{
	public class FxOrientedAppearance : FxBasicAppearance
	{
		public AlchemyFloatAnimation? Height;
		public AlchemyFloatAnimation? Width;

		public FxOrientedAppearance(AlchemyNode ale) : base(ale)
        {
            Height = ale.GetFloatAnimation(AleProperty.OrientedApp_Height);
            Width = ale.GetFloatAnimation(AleProperty.OrientedApp_Width);
		}

		public FxOrientedAppearance(string name) : base(name)
        {
            Size = null!;
            Width = new(1);
            Height = new(1);
        }

        internal static Vector2 GetOrientedSize(float width, float height) => new Vector2(width, height) * 2;

        public override void Draw(ParticleEffectInstance instance, AppearanceReference node, int nodeIdx,
            Matrix4x4 transform, float sparam)
        {
            var count = instance.Buffer.GetCount(nodeIdx);
            TextureHandler.Update(Texture, instance.Resources!);
            var nodeTr = instance.NodeWorldTransforms[node.NodeIdx];

            for (var i = 0; i < count; i++)
            {
                ref var particle = ref instance.Buffer[nodeIdx, i];
                var time = particle.TimeAlive / particle.LifeSpan;
                var position = Vector3.Transform(Vector3.Transform(particle.Position, particle.Orientation), nodeTr);
                if (!PassesVisibilityFields(node, instance, position, sparam))
                    continue;
                var velocity = Vector3.TransformNormal(particle.Velocity, nodeTr);
                var color = Color.GetValue(sparam, time);
                var alpha = Alpha.GetValue(sparam, time);
                var width = Width?.GetValue(sparam, time) ?? 1f;
                var height = Height?.GetValue(sparam, time) ?? 1f;

                instance.Pool?.AddParticle(
                    TextureHandler,
                    position,
                    GetOrientedSize(width, height),
                    new Color4(color, alpha),
                    GetFrame((float)instance.GlobalTime, sparam, ref particle),
                    velocity,
                    Rotate == null ? 0f : MathHelper.DegreesToRadians(Rotate.GetValue(sparam, time)),
                    FlipHorizontal,
                    FlipVertical,
                    MotionBlur
                );
            }

            instance.Pool?.DrawBuffer(ParticleDrawKind.Basic, this, instance.Resources!, transform,
                (instance.DrawIndex << 11) + nodeIdx);
        }

        public override AlchemyNode SerializeNode()
        {
            var n = SerializeBaseNode();
            SerializeColorTextureParameters(n);
            n.Parameters.Add(new(AleProperty.OrientedApp_Height, Height!));
            n.Parameters.Add(new(AleProperty.OrientedApp_Width, Width!));
            return n;
        }
	}
}
