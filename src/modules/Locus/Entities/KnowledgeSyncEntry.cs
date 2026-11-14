using Sentium.Core.Common;

namespace Sentium.Locus.Entities;

/// <summary>
/// Outbox row saying "this asset's knowledge-base copy must be brought up to date". Written in the same
/// transaction as the change that caused it, so no sync is lost on a crash. Deliberately has no foreign key
/// to <see cref="Asset"/>: a removal request must outlive the asset's hard delete.
/// </summary>
public sealed class KnowledgeSyncEntry : INonAudited
{
    public Guid AssetId { get; set; }

    public KnowledgeSyncStatus Status { get; set; }

    /// <summary>
    /// Bumped on every new request; the processor only deletes the entry if it still holds the version it
    /// processed, so an edit made while ingestion was running is never lost.
    /// </summary>
    public int Version { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? LastError { get; set; }
}
