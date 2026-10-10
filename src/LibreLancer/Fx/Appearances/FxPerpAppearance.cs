// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Numerics;
using LibreLancer.Render.Materials;
using LibreLancer.Utf.Ale;
namespace LibreLancer.Fx
{
	public class FxPerpAppearance : FxBasicAppearance
	{
		public bool ViewingAngleFade;

		public FxPerpAppearance(AlchemyNode ale) : base(ale)
		{
			ViewingAngleFade = ale.GetBoolean(AleProperty.RectApp_ViewingAngleFade);
		}

        public FxPerpAppearance(string name) : base(name)
        {
        }

        internal static Vector3 GetDirection(Vector3 direction, Vector3 fallbackDirection)
        {
            var lengthSquared = direction.LengthSquared();
            if (float.IsFinite(lengthSquared) && lengthSquared > float.Epsilon)
                return direction / MathF.Sqrt(lengthSquared);

            lengthSquared = fallbackDirection.LengthSquared();
            if (float.IsFinite(lengthSquared) && lengthSquared > float.Epsilon)
                return fallbackDirection / MathF.Sqrt(lengthSquared);

            return Vector3.UnitY;
        }

        internal static Vector3 TransformPosition(Vector3 position, Quaternion orientation, Matrix4x4 nodeTransform) =>
            Vector3.Transform(Vector3.Transform(position, orientation), nodeTransform);

        internal static Vector3 TransformDirection(Vector3 direction, Quaternion orientation,
            Matrix4x4 nodeTransform) =>
            Vector3.TransformNormal(Vector3.Transform(direction, orientation), nodeTransform);

        public override void Draw(ParticleEffectInstance instance, AppearanceReference node, int nodeIdx,
            Matrix4x4 transform, float sparam)
        {
            var count = instance.Buffer.GetCount(nodeIdx);
            TextureHandler.Update(Texture, instance.Resources);
            var node_tr = instance.NodeWorldTransforms[node.NodeIdx];
            for (int i = 0; i < count; i++)
            {
                ref var particle = ref instance.Buffer[nodeIdx, i];
                var time = particle.TimeAlive / particle.LifeSpan;

                var c = Color.GetValue(sparam, time);
                var a = Alpha.GetValue(sparam, time);
                var q = particle.Orientation * Transform.GetDeltaRotation(sparam,
                    (float)instance.LastTime, (float)instance.GlobalTime);
                particle.Orientation = q;
                var p = TransformPosition(particle.Position, q, node_tr);
                if (!PassesVisibilityFields(node, instance, p, sparam))
                    continue;
                var p2 = p + TransformDirection(particle.Velocity, q, node_tr);
                var fallbackDirection = TransformDirection(particle.Normal, q, node_tr);
                var n = GetDirection(p - p2, fallbackDirection);
                if (ViewingAngleFade)
                    a *= GetViewingAngleFade(n, instance.Pool.Camera.Position - p);
                instance.Pool.AddParticle(
                    TextureHandler,
                    p,
                    new Vector2(Size.GetValue(sparam, time)),
                    new Color4(c, a),
                    GetFrame((float)instance.GlobalTime, sparam, ref particle),
                    n,
                    Rotate == null ? 0f : MathHelper.DegreesToRadians(Rotate.GetValue(sparam, time)),
                    FlipHorizontal, FlipVertical
                );

                if (DrawDebug)
                {
                    Debug?.DrawLine(p - (n * 100), p + (n * 100), Color4.Red);
                }
            }
            instance.Pool.DrawBuffer(
                ParticleDrawKind.Perp,
                this,
                instance.Resources,
                transform,
                (instance.DrawIndex << 11) + nodeIdx
            );
        }
	}
}
