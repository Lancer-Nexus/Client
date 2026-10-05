using System;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcTransferGroupReservationsTests
{
    [Fact]
    public void ReservesWholeTraderAndEscortGroupAndTreatsRepeatedLeaderCallbackAsDuplicate()
    {
        var reservations = new NpcTransferGroupReservations();
        var transferId = Guid.NewGuid();
        var trader = Guid.NewGuid();
        var escort = Guid.NewGuid();

        Assert.Equal(NpcTransferReservationResult.Reserved,
            reservations.TryReserve(transferId, [trader, escort]));
        Assert.Equal(NpcTransferReservationResult.AlreadyReserved,
            reservations.TryReserve(transferId, [trader, escort]));
    }

    [Fact]
    public void RejectsOverlappingGroupWithoutReservingItsOtherMembers()
    {
        var reservations = new NpcTransferGroupReservations();
        var trader = Guid.NewGuid();
        var escort = Guid.NewGuid();
        var unrelated = Guid.NewGuid();

        Assert.Equal(NpcTransferReservationResult.Reserved,
            reservations.TryReserve(Guid.NewGuid(), [trader, escort]));
        Assert.Equal(NpcTransferReservationResult.Conflict,
            reservations.TryReserve(Guid.NewGuid(), [escort, unrelated]));
        Assert.Equal(NpcTransferReservationResult.Reserved,
            reservations.TryReserve(Guid.NewGuid(), [unrelated]));
    }

    [Fact]
    public void ReleasesAllMembersWhenTransferFinishesOrRollsBack()
    {
        var reservations = new NpcTransferGroupReservations();
        var transferId = Guid.NewGuid();
        var trader = Guid.NewGuid();
        var escort = Guid.NewGuid();
        reservations.TryReserve(transferId, [trader, escort]);

        reservations.Release(transferId);

        Assert.Equal(NpcTransferReservationResult.Reserved,
            reservations.TryReserve(Guid.NewGuid(), [trader, escort]));
    }
}
