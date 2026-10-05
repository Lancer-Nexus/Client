// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Numerics;
using LibreLancer.Utf.Ale;
namespace LibreLancer.Fx
{
	public class FLDustField : FxField
	{
        public AlchemyCurveAnimation MaxRadius;
        public AleProperty MaxRadiusProperty;

        public FLDustField (AlchemyNode ale) : base(ale)
        {
            MaxRadiusProperty = ale.TryGetParameter(AleProperty.DustField_MaxRadius, out _)
                ? AleProperty.DustField_MaxRadius
                : AleProperty.SphereEmitter_MaxRadius;
            MaxRadius = ale.GetCurveAnimation(MaxRadiusProperty)!;
        }

        public FLDustField(string name) : base(name)
        {
            MaxRadiusProperty = AleProperty.DustField_MaxRadius;
            MaxRadius = new(1);
        }

        public override AlchemyNode SerializeNode()
        {
            var node = base.SerializeNode();
            node.Parameters.Add(new(MaxRadiusProperty, MaxRadius));
            return node;
        }

        public bool IsPositionVisible(ParticleEffectInstance instance, FieldReference field,
            Vector3 worldPosition, float sparam)
        {
            var radius = MaxRadius.GetValue(sparam, (float) instance.GlobalTime);
            var center = Vector3.Transform(Vector3.Zero, instance.NodeWorldTransforms[field.NodeIdx]);
            return IsWithinRadius(worldPosition, center, radius);
        }

        internal static bool IsWithinRadius(Vector3 position, Vector3 center, float radius)
        {
            if (!float.IsFinite(radius) || radius < 0)
                return false;
            var offset = position - center;
            return offset.LengthSquared() <= radius * radius;
        }
	}
}

