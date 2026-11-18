namespace Sentium.Memoria.Features.Notes;

/// <param name="DeviceName">The paired Device that syncs the vault; <see langword="null"/> once that Device is removed.</param>
/// <param name="PendingIndexCount">Notes waiting to be embedded.</param>
/// <param name="FailedIndexCount">Notes whose embedding gave up after retries; they are retried on their next change.</param>
internal sealed record VaultSummaryDto(
    Guid Id,
    string Name,
    string? DeviceName,
    int NoteCount,
    int LinkCount,
    int UnresolvedLinkCount,
    int OrphanCount,
    int PendingIndexCount,
    int FailedIndexCount,
    DateTimeOffset? LastSyncAt);
