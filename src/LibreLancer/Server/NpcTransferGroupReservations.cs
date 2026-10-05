using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreLancer.Server;

internal enum NpcTransferReservationResult
{
    Reserved,
    AlreadyReserved,
    Conflict
}

/// <summary>Prevents escorts from starting duplicate transfers while their trade group is leaving.</summary>
internal sealed class NpcTransferGroupReservations
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, Guid> npcTransfers = [];

    public NpcTransferReservationResult TryReserve(Guid transferId, IEnumerable<Guid> npcIds)
    {
        if (transferId == Guid.Empty)
            throw new ArgumentException("A transfer ID is required.", nameof(transferId));
        var ids = npcIds.Distinct().ToArray();
        if (ids.Length == 0 || ids.Any(id => id == Guid.Empty))
            throw new ArgumentException("A transfer group must contain valid NPC IDs.", nameof(npcIds));

        lock (sync)
        {
            var existing = ids.Where(npcTransfers.ContainsKey)
                .Select(id => npcTransfers[id])
                .Distinct()
                .ToArray();
            if (existing.Length > 0)
                return existing.Length == 1 && existing[0] == transferId && ids.All(npcTransfers.ContainsKey)
                    ? NpcTransferReservationResult.AlreadyReserved
                    : NpcTransferReservationResult.Conflict;

            foreach (var id in ids)
                npcTransfers.Add(id, transferId);
            return NpcTransferReservationResult.Reserved;
        }
    }

    public void Release(Guid transferId)
    {
        lock (sync)
        {
            foreach (var id in npcTransfers.Where(pair => pair.Value == transferId).Select(pair => pair.Key).ToArray())
                npcTransfers.Remove(id);
        }
    }
}
