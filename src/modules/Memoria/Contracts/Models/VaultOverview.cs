namespace Sentium.Memoria.Contracts.Models;

/// <param name="Orphans">A sample of notes with no links in or out.</param>
/// <param name="UnresolvedTargets">The most linked targets that match no synced note.</param>
/// <param name="PendingIndexCount">Notes waiting to be (re)embedded; search misses their latest text until then.</param>
public sealed record VaultOverview(
    string Name,
    int NoteCount,
    int LinkCount,
    DateTimeOffset? LastSyncAt,
    IReadOnlyList<VaultTagCount> TopTags,
    int OrphanCount,
    IReadOnlyList<VaultNoteRef> Orphans,
    int UnresolvedLinkCount,
    IReadOnlyList<string> UnresolvedTargets,
    int PendingIndexCount);
