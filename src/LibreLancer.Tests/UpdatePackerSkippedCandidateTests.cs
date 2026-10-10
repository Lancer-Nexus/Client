using System;
using System.Linq;
using LibreLancer.Net;
using LibreLancer.Net.Protocol;
using LibreLancer.Server;
using LibreLancer.Server.Components;
using LibreLancer.World;
using LibreLancer.World.Components;
using Xunit;

namespace LibreLancer.Tests;

public class UpdatePackerSkippedCandidateTests
{
    [Fact]
    public void OversizedFirstCandidateDoesNotChangeIdsOrBreakAcknowledgedDeltas()
    {
        var selfObject = new GameObject();
        var self = new SPlayerComponent(new Player(null!, null!, Guid.Empty), selfObject);
        var objects = new[] { new GameObject(), new GameObject() };
        var random = new Random(42);
        var large = new ObjectUpdate
        {
            ID = new(-43),
            Guns = Enumerable.Range(0, 100).Select(_ => new GunOrient
            {
                AnglePitch = (float)random.NextDouble() * 3,
                AngleRot = (float)random.NextDouble() * 3
            }).ToArray()
        };
        var small = new ObjectUpdate { ID = new(-45), Hull = 100 };
        var packer = new UpdatePacker();
        var first = packer.Begin([large, small], objects).Pack(10, default, self, selfObject, 128);
        var decoded = first.GetUpdates(default, (_, _) => throw new Exception("Unexpected initial delta"));
        var update = Assert.Single(decoded.Updates);
        Assert.Equal(-45, update.ID.Value);
        self.MostRecentAck = new UpdateAck(10, 0);
        small = new ObjectUpdate { ID = new(-45), Hull = 99 };
        var next = packer.Begin([large, small], objects).Pack(11, default, self, selfObject, 128);
        var nextDecoded = next.GetUpdates(decoded.AuthState, (tick, id) =>
        {
            Assert.Equal(10u, tick);
            return decoded.Updates.Single(previous => previous.ID.Value == id);
        });
        Assert.Equal(-45, Assert.Single(nextDecoded.Updates).ID.Value);
        Assert.Equal(99, nextDecoded.Updates[0].Hull);
    }
}
