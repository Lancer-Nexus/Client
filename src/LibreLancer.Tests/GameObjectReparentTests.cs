using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public sealed class GameObjectReparentTests
{
    [Fact]
    public void ReparentMovesChildBetweenParentCollections()
    {
        var previousParent = new GameObject();
        var nextParent = new GameObject();
        var child = new GameObject { Parent = previousParent };
        previousParent.Children.Add(child);

        child.Reparent(nextParent);

        Assert.DoesNotContain(child, previousParent.Children);
        Assert.Same(nextParent, child.Parent);
        Assert.Contains(child, nextParent.Children);
    }

    [Fact]
    public void ReparentDoesNotDuplicateChildWhenParentIsUnchanged()
    {
        var parent = new GameObject();
        var child = new GameObject { Parent = parent };
        parent.Children.Add(child);

        child.Reparent(parent);

        Assert.Same(child, Assert.Single(parent.Children));
        Assert.Same(parent, child.Parent);
    }
}
