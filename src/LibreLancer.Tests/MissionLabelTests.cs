using LibreLancer.Missions;
using Xunit;

namespace LibreLancer.Tests;

public class MissionLabelTests
{
    [Fact]
    public void AnyAliveRemainsTrueUntilLastSpawnedShipIsDestroyed()
    {
        var label = new MissionLabel("enemies", ["enemy1", "enemy2"]);
        label.Spawned("enemy1");
        label.Spawned("enemy2");

        label.Destroyed("enemy1");
        Assert.True(label.AnyAlive());

        label.Destroyed("enemy2");
        Assert.False(label.AnyAlive());
    }

    [Fact]
    public void AllKilledOnlyConsidersSpawnedLabelMembers()
    {
        var label = new MissionLabel("enemies", ["enemy1", "enemy2"]);
        label.Spawned("enemy1");
        label.Destroyed("enemy1");

        Assert.True(label.IsAllKilled());

        label.Spawned("enemy2");
        Assert.False(label.IsAllKilled());

        label.Destroyed("enemy2");
        Assert.True(label.IsAllKilled());
    }

    [Fact]
    public void AllKilledRequiresAtLeastOneSpawnedMember()
    {
        var label = new MissionLabel("enemies", ["enemy1", "enemy2"]);

        Assert.False(label.IsAllKilled());
    }

    [Fact]
    public void TransferStateRestoresAliveDestroyedAndNotSpawnedMembers()
    {
        var source = new MissionLabel("enemies", ["alive", "destroyed", "not_spawned"]);
        source.Spawned("alive");
        source.Spawned("destroyed");
        source.Destroyed("destroyed");

        var target = new MissionLabel("enemies", ["alive", "destroyed", "not_spawned"]);
        target.RestoreTransferState(source.CaptureTransferState());

        Assert.True(target.AnyAlive());
        Assert.Equal(1, target.DestroyedCount());
        Assert.False(target.IsAllKilled());
        target.Destroyed("alive");
        Assert.True(target.IsAllKilled());
    }
}
