using System;
using System.IO;
using System.Linq;
using LibreLancer.Data.GameData;
using LibreLancer.Data.Ini;
using LibreLancer.Data.IO;
using LibreLancer.Data.Schema;
using LibreLancer.Data.Schema.Universe;
using LibreLancer.Render;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ColorGraphTests
{
    [Fact]
    public void GraphIniLoadsColorGraphsWithoutDroppingFloatGraphs()
    {
        var root = Path.Combine(Path.GetTempPath(), "igraph-color-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "igraph.ini"), """
                [IGraph]
                nickname = existing_float
                type = FLOAT
                point = 0, 1.0
                point = 1, 0.0

                [IGraph]
                nickname = lair_flicker
                type = COLOR
                point = 1, -6238016
                point = 3, -4128800
                point = 5, -6238016
                """);

            var graphs = new GraphIni();
            graphs.AddGraphIni("igraph.ini", FileSystem.FromPath(root));

            Assert.Single(graphs.FloatGraphs);
            var graph = Assert.IsType<ColorGraph>(graphs.FindColorGraph("LAIR_FLICKER"));
            Assert.Equal(3, graph.Points.Count);
            Assert.Equal(0xffa0d0c0u, graph.Points[0].Color);
            Assert.Equal(0xffc0ffe0u, graph.Points[1].Color);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LightSourceParsesColorCurveNameAndPeriod()
    {
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            [LightSource]
            nickname = lair_light
            color_curve = lair_flicker, 0.500000
            """));
        var section = Assert.Single(IniFile.ParseFile("light.ini", input, true, false));

        Assert.True(LightSource.TryParse(section, out var source));
        Assert.Equal("lair_flicker", source!.ColorCurve);
        Assert.Equal(0.5f, source.ColorCurvePeriod);
    }

    [Fact]
    public void ColorGraphCyclesAndModulatesStaticLightColor()
    {
        var graph = new ColorGraph
        {
            Name = "flicker",
            Points =
            [
                new ColorGraphPoint(0, 0xff000000),
                new ColorGraphPoint(10, 0xffffffff)
            ]
        };
        var light = new DynamicLight
        {
            Light = new RenderLight { Color = new Color3f(1, 0.5f, 0.25f) },
            BaseColor = new Color3f(1, 0.5f, 0.25f),
            ColorCurve = graph,
            ColorCurvePeriod = 0.5f
        };

        light.UpdateColorCurve(0.25);
        Assert.Equal(128 / 255f, light.Light.Color.R, 0.00001f);
        Assert.Equal(64 / 255f, light.Light.Color.G, 0.00001f);
        Assert.Equal(32 / 255f, light.Light.Color.B, 0.00001f);

        light.UpdateColorCurve(0.5);
        Assert.Equal(Color3f.Black, light.Light.Color);
    }
}
