using System;
using System.IO;
using System.Linq;
using LibreLancer.Thn;
using LibreLancer.Thn.Events;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ThnIntroParticleOrientationTests
{
    [Fact]
    public void PlanetChunksShipExhaustsFollowHardpointOrientation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "patches", "series")))
            root = root.Parent;

        Assert.NotNull(root);
        var thnPath = Path.Combine(root!.FullName, "data", "SCRIPTS", "INTRO", "intro_planetchunks.thn");
        var script = new ThnScript(File.ReadAllBytes(thnPath), _ => System.Array.Empty<byte>(), thnPath);

        foreach (var target in new[]
                 {
                     (Effect: "Intro_planetchunk_gf_br_luxury_engine01_3", Ship: "ge_liner_4"),
                     (Effect: "Intro_planetchunk_gf_br_luxury_engine01_4", Ship: "ge_liner_5")
                 })
        {
            var attachment = Assert.Single(script.Events.OfType<AttachEntityEvent>(), e =>
                e.Targets.SequenceEqual(new[] { target.Effect, target.Ship }));
            Assert.Equal(TargetTypes.Hardpoint, attachment.TargetType);
            Assert.Equal("hpengine01", attachment.TargetPart);
            Assert.Equal(AttachFlags.Position | AttachFlags.Orientation, attachment.Flags);
        }
    }
}
