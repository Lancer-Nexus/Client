using LibreLancer;
using LibreLancer.Render.Materials;
using LibreLancer.Resources;
using LibreLancer.Utf.Mat;
using Xunit;

namespace LibreLancer.Tests;

public class GlowmapEmissionTests
{
    [Fact]
    public void GlowmapWithoutEmissionColorDefaultsToWhiteMask()
    {
        var material = new Material((ResourceManager)null!) { Type = "DcDtEt", EtName = "glow" };

        material.Initialize(null!);

        Assert.Equal(Color4.White, Assert.IsType<BasicMaterial>(material.Render).Ec);
    }

    [Fact]
    public void ExplicitEmissionColorIsPreservedForGlowmapMask()
    {
        var emission = new Color4(0.25f, 0.5f, 0.75f, 1f);
        var material = new Material((ResourceManager)null!) { Type = "DcDtEcEt", EtName = "glow", Ec = emission };

        material.Initialize(null!);

        Assert.Equal(emission, Assert.IsType<BasicMaterial>(material.Render).Ec);
    }
}
