using LibreLancer.Render;
using Xunit;

namespace LibreLancer.Tests;

public sealed class ModelRendererLodTests
{
    [Fact]
    public void UsesMeshSwitchDistancesWhenTheyDifferFromArchetypeRanges()
    {
        float[] modelRanges = [100, 200, 300, 400, 500, 600];
        float[] meshRanges = [50, 150];

        var ranges = ModelRenderer.SelectLodRanges(modelRanges, meshRanges);

        Assert.Same(meshRanges, ranges);
    }

    [Fact]
    public void FallsBackToArchetypeRangesWhenMeshHasNoSwitchDistances()
    {
        float[] modelRanges = [100, 200, 300];

        Assert.Same(modelRanges, ModelRenderer.SelectLodRanges(modelRanges, null));
        Assert.Same(modelRanges, ModelRenderer.SelectLodRanges(modelRanges, []));
    }

    [Fact]
    public void MaximumLodDistanceIncludesMeshRangesBeyondArchetypeCullRange()
    {
        float[] modelRanges = [100, 200, 300];
        float[] meshRanges = [250, 450];

        var maximum = ModelRenderer.MaximumLodDistance(modelRanges, new float[]?[] { meshRanges });

        Assert.Equal(450, maximum);
    }
}
