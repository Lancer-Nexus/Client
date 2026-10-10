// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using LibreLancer.Data.GameData;
using LibreLancer.Data.Schema;

namespace LibreLancer.Render
{
	public class DynamicLight
	{
		public int LightGroup = 0;
		public bool Active = true;
		public RenderLight Light;
		public Color3f BaseColor = Color3f.White;
		public ColorGraph? ColorCurve;
		public float ColorCurvePeriod;

		public void UpdateColorCurve(double time)
		{
			if (ColorCurve == null)
				return;
			var packed = ColorCurve.EvaluatePackedColor(time, ColorCurvePeriod);
			var curveColor = new Color3f(
				((packed >> 16) & 0xff) / 255f,
				((packed >> 8) & 0xff) / 255f,
				(packed & 0xff) / 255f);
			var light = Light;
			light.Color = BaseColor * curveColor;
			Light = light;
		}
	}
}
