using System.Text;
using LibreLancer.Data.Schema;
using LibreLancer.Utf;
using LibreLancer.Utf.Mat;
using Xunit;

namespace LibreLancer.Tests;

public class MaterialTypeTests
{
    [Theory]
    [InlineData("DcDtBt")]
    [InlineData("DcDtBtOcOt")]
    [InlineData("DcDtEcEt")]
    public void BasicMaterialVariantsAreLoaded(string type)
    {
        _ = new MaterialMap();
        var material = new IntermediateNode("test_material",
        [new LeafNode("type", Encoding.ASCII.GetBytes(type + "\0"))]);
        var library = new IntermediateNode("material library", [material]);

        var matFile = new MatFile(library);

        var loaded = Assert.Single(matFile.Materials.Values);
        Assert.Equal(type, loaded.Type);
        Assert.Equal("test_material", loaded.Name);
    }
}
