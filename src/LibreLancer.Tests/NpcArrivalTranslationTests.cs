using System;
using System.Numerics;
using LancerNexus.Protocol;
using LibreLancer.Server;
using MessagePack;
using Xunit;

namespace LibreLancer.Tests;

public class NpcArrivalTranslationTests
{
    [Fact]
    public void MissionGroupArrivesAtGateWithItsOffsetsAndRuntimeIntact()
    {
        var leader = Guid.NewGuid();
        var follower = Guid.NewGuid();
        var missionState = new byte[] { 1, 2, 3 };
        var first = new NpcRuntimeStateV1
        {
            Position = new() { X = 10, Y = 20, Z = 30 },
            LinearVelocity = new() { X = 5, Y = 6, Z = 7 }, Health = 42,
            MissionState = missionState
        };
        var second = new NpcRuntimeStateV1 { Position = new() { X = 60, Y = -30, Z = 30 } };
        var snapshot = new NpcTransferSnapshot
        {
            TransferId = Guid.NewGuid(), MissionRuntimeId = Guid.NewGuid(),
            TargetSystemId = "li03", TargetArrivalObject = "Li03_to_Li01",
            NpcIds = [leader, follower], MissionRuntimeState = missionState,
            Npcs = [new() { NpcId = leader, OwnershipVersion = 1, RuntimeState = MessagePackSerializer.Serialize(first) },
                     new() { NpcId = follower, OwnershipVersion = 1, RuntimeState = MessagePackSerializer.Serialize(second) }],
            Formations = [new() { Members = [new() { NpcId = leader, IsLeader = true },
                                            new() { NpcId = follower, Offset = new() { X = 50, Y = -50 } }] }]
        };
        var arriving = GameServer.RebaseNpcSnapshotAtArrival(snapshot, new Vector3(1000, 2000, 3000));
        var lead = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(arriving.Npcs[0].RuntimeState);
        var escort = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(arriving.Npcs[1].RuntimeState);
        Assert.Equal((1000f, 2000f, 3000f), (lead.Position.X, lead.Position.Y, lead.Position.Z));
        Assert.Equal((1050f, 1950f, 3000f), (escort.Position.X, escort.Position.Y, escort.Position.Z));
        Assert.Equal((5f, 6f, 7f), (lead.LinearVelocity.X, lead.LinearVelocity.Y, lead.LinearVelocity.Z));
        Assert.Equal(42f, lead.Health);
        Assert.Equal(first.MissionState, lead.MissionState);
        Assert.Equal(snapshot.TransferId, arriving.TransferId);
        Assert.Equal(snapshot.MissionRuntimeId, arriving.MissionRuntimeId);
        Assert.Same(snapshot.MissionRuntimeState, arriving.MissionRuntimeState);
        Assert.Same(snapshot.Formations, arriving.Formations);
        Assert.Equal(1, arriving.Npcs[0].OwnershipVersion);
        Assert.Equal("li03", arriving.Npcs[0].SystemId);
        Assert.Equal(leader, arriving.Npcs[0].NpcId);
        Assert.Equal(10f, MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(snapshot.Npcs[0].RuntimeState).Position.X);
    }

    [Fact]
    public void RestoreWithoutArrivalObjectKeepsOriginalCoordinates()
    {
        var snapshot = new NpcTransferSnapshot();
        Assert.Same(snapshot, GameServer.RebaseNpcSnapshotAtArrival(snapshot, Vector3.One));
    }
}
