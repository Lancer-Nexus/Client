using System;
using LancerNexus.Protocol;

namespace LibreLancer.Server;

internal static class NpcMissionTransferCommitGate
{
    public static bool IsCommittedByCharacterTransfer(Guid transferId, string targetInstanceId,
        TransferSnapshotImportRecord? import, TransferStatusResponse status, NpcTransferSnapshot snapshot)
    {
        if (transferId == Guid.Empty || string.IsNullOrWhiteSpace(targetInstanceId) ||
            import is not { Imported: true } || import.TransferId != transferId ||
            import.LeaseVersion < 0 || import.LeaseVersion == long.MaxValue ||
            snapshot.TransferId != transferId || snapshot.MissionRuntimeId != transferId ||
            !string.Equals(snapshot.TargetSystemId, import.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
            status.TransferId != transferId ||
            !string.Equals(status.SourceInstanceId, import.SourceInstanceId, StringComparison.Ordinal) ||
            !string.Equals(status.TargetInstanceId, targetInstanceId, StringComparison.Ordinal) ||
            !string.Equals(status.TargetInstanceId, import.TargetInstanceId, StringComparison.Ordinal) ||
            !string.Equals(status.TargetSystemId, import.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
            status.LeaseVersion != import.LeaseVersion + 1)
            return false;

        return status.State is TransferState.Committed or TransferState.SourceReleased;
    }
}
