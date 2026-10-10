using System;
using System.Numerics;
using System.Reflection;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public class FailedCharacterEntryPositionTests
{
    [Fact]
    public void InitialRpcFailureCannotLeaveDisconnectWithAnEmptyLocation()
    {
        var player = new Player(null!, null!, Guid.Empty);
        var character = new NetCharacter { Name = "fixture" };
        using (var transaction = character.BeginTransaction())
            transaction.UpdatePosition("Li01_01_Base", "li01", new Vector3(1, 2, 3), Quaternion.Zero);
        // The missing transport intentionally fails the first initial RPC.
        var begin = typeof(Player).GetMethod("BeginGame", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Throws<TargetInvocationException>(() => begin.Invoke(player, [character, null]));
        Assert.Same(character, player.Character);
        Assert.Equal("li01", player.System);
        Assert.Equal("Li01_01_Base", player.Base);
        Assert.Equal(new Vector3(1, 2, 3), player.Position);
        Assert.Equal(Quaternion.Identity, player.Orientation);
    }
}
