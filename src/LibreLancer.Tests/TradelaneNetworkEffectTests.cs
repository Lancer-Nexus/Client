using LibreLancer.Server.Components;
using LibreLancer.World;
using Xunit;

namespace LibreLancer.Tests;

public class TradelaneNetworkEffectTests
{
    [Fact]
    public void NetworkEffectCanBeRemovedAtLaneExit()
    {
        var runner = new SFuseRunnerComponent(new GameObject());

        var id = runner.AddNetworkEffect("basic_tl_ship_travel", []);
        Assert.Collection(runner.Effects, effect =>
        {
            Assert.Equal(id, effect.ID);
            Assert.Equal("basic_tl_ship_travel", effect.Effect);
            Assert.Empty(effect.Hardpoints);
        });

        Assert.True(runner.RemoveNetworkEffect(id));
        Assert.Empty(runner.Effects);
        Assert.False(runner.RemoveNetworkEffect(id));
    }
}
