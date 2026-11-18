using Sentium.Core.Common;

namespace Sentium.Memoria.Entities;

/// <summary>
/// Outbox row saying "this note's knowledge-base copy must be brought up to date". Written in the same transaction as
/// the sync that caused it. It has no foreign key to <see cref="VaultNote"/> so a removal outlives the note.
/// </summary>
internal sealed class NoteIndexEntry : INonAudited
{
    public Guid NoteId { get; set; }

    public Guid UserId { get; set; }

    public NoteIndexStatus Status { get; set; }

    /// <summary>
    /// Bumped on every new request; the processor only deletes the entry if it still holds the version it processed,
    /// so a sync that lands while a note is being embedded is never lost.
    /// </summary>
    public int Version { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? LastError { get; set; }
}
