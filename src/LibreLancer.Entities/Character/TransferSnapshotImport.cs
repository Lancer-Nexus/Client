using System;

namespace LibreLancer.Entities.Character;

/// <summary>Durable target-side state for an idempotent character transfer import.</summary>
public sealed class TransferSnapshotImport
{
    public Guid TransferId { get; set; }
    public Guid AccountId { get; set; }
    public long CharacterId { get; set; }
    public string SourceInstanceId { get; set; } = "";
    public string TargetInstanceId { get; set; } = "";
    public string TargetSystemId { get; set; } = "";
    public long LeaseVersion { get; set; }
    public string TicketHash { get; set; } = "";
    public string? SnapshotHash { get; set; }
    public bool Imported { get; set; }
}
