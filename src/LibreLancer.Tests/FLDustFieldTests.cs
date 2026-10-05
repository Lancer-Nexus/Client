using System.Numerics;
using LibreLancer.Fx;
using LibreLancer.Utf.Ale;
using Xunit;

namespace LibreLancer.Tests;

public sealed class FLDustFieldTests
{
    [Fact]
    public void ReadsTheRadiusCurveUsedByVanillaDustFields()
    {
        var radius = new AlchemyCurveAnimation(12f);
        var node = new AlchemyNode();
        node.Parameters.Add(new AleParameter(AleProperty.DustField_MaxRadius, radius));

        var field = new FLDustField(node);

        Assert.Same(radius, field.MaxRadius);
    }

    [Fact]
    public void ReadsTheSphereEmitterRadiusAliasUsedByMostVanillaDustFields()
    {
        var radius = new AlchemyCurveAnimation(60.1f);
        var node = new AlchemyNode();
        node.Parameters.Add(new AleParameter(AleProperty.SphereEmitter_MaxRadius, radius));

        var field = new FLDustField(node);

        Assert.Same(radius, field.MaxRadius);
        Assert.Equal(AleProperty.SphereEmitter_MaxRadius, field.MaxRadiusProperty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesTheRadiusPropertyKeyWhenSerializing(bool useDustFieldKey)
    {
        var radius = new AlchemyCurveAnimation(12f);
        var key = useDustFieldKey ? AleProperty.DustField_MaxRadius : AleProperty.SphereEmitter_MaxRadius;
        var node = new AlchemyNode();
        node.Parameters.Add(new AleParameter(key, radius));
        var field = new FLDustField(node);

        var serialized = field.SerializeNode();

        Assert.Contains(serialized.Parameters,
            p => p.Name == key && ReferenceEquals(p.Value, radius));
    }

    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(3, 4, 0, true)]
    [InlineData(3.001, 4, 0, false)]
    [InlineData(0, 0, 5.001, false)]
    public void VisibilityIsLimitedToTheDustSphere(double x, double y, double z, bool expected)
    {
        Assert.Equal(expected, FLDustField.IsWithinRadius(
            new Vector3((float)x, (float)y, (float)z), Vector3.Zero, 5));
    }
}
