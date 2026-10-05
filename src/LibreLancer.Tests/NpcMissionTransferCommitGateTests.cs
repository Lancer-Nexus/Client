using System;
using LancerNexus.Protocol;
using LibreLancer.Server;
using Xunit;

namespace LibreLancer.Tests;

public sealed class NpcMissionTransferCommitGateTests
{
    [Fact]
    public void AllowsNpcCommitOnlyAfterMatchingCharacterLeaseCommit()
    {
        var transferId = Guid.NewGuid();
        var import = new TransferSnapshotImportRecord(transferId, Guid.NewGuid(), 42,
            "source-01", "target-01", "li03", 8, "ticket", "snapshot", true);
        var snapshot = new NpcTransferSnapshot
        {
            TransferId = transferId,
            MissionRuntimeId = transferId,
            TargetSystemId = "LI03"
        };
        var status = new TransferStatusResponse
        {
            TransferId = transferId,
            SourceInstanceId = "source-01",
            TargetInstanceId = "target-01",
            TargetSystemId = "li03",
            State = TransferState.Committed,
            LeaseVersion = 9
        };

        Assert.True(NpcMissionTransferCommitGate.IsCommittedByCharacterTransfer(
            transferId, "target-01", import, status, snapshot));
    }

    [Theory]
    [InlineData(TransferState.TargetAccepted, 9)]
    [InlineData(TransferState.Committed, 8)]
    [InlineData(TransferState.SourceReleased, 10)]
    public void RejectsUncommittedOrMismatchedCharacterTransfer(TransferState state, long leaseVersion)
    {
        var transferId = Guid.NewGuid();
        var import = new TransferSnapshotImportRecord(transferId, Guid.NewGuid(), 42,
            "source-01", "target-01", "li03", 8, "ticket", "snapshot", true);
        var snapshot = new NpcTransferSnapshot
        {
            TransferId = transferId,
            MissionRuntimeId = transferId,
            TargetSystemId = "li03"
        };
        var status = new TransferStatusResponse
        {
            TransferId = transferId,
            SourceInstanceId = "source-01",
            TargetInstanceId = "target-01",
            TargetSystemId = "li03",
            State = state,
            LeaseVersion = leaseVersion
        };

        Assert.False(NpcMissionTransferCommitGate.IsCommittedByCharacterTransfer(
            transferId, "target-01", import, status, snapshot));
    }

    [Fact]
    public void AutonomousPopulationNpcTransferCannotPassCharacterCommitGate()
    {
        var transferId = Guid.NewGuid();
        var import = new TransferSnapshotImportRecord(transferId, Guid.NewGuid(), 42,
            "source-01", "target-01", "li03", 8, "ticket", "snapshot", true);
        var snapshot = new NpcTransferSnapshot
        {
            TransferId = transferId,
            TargetSystemId = "li03"
        };
        var status = new TransferStatusResponse
        {
            TransferId = transferId,
            SourceInstanceId = "source-01",
            TargetInstanceId = "target-01",
            TargetSystemId = "li03",
            State = TransferState.Committed,
            LeaseVersion = 9
        };

        Assert.False(NpcMissionTransferCommitGate.IsCommittedByCharacterTransfer(
            transferId, "target-01", import, status, snapshot));
    }
}
