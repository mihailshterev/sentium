using Sentium.Memoria.Contracts.Models;

namespace Sentium.Memoria.Contracts;

/// <summary>
/// Read-only view of a user's synced Obsidian vault for assistants. Needs no HTTP context: every call names the user
/// whose vault it reads, and nothing else is ever visible. Notes are identified by vault path, by path without
/// <c>.md</c>, by title or by alias, resolved the way an Obsidian wikilink would be.
/// </summary>
public interface IVaultAgentAccess
{
    /// <summary>
    /// <see langword="null"/> when the user has not synced a vault.
    /// </summary>
    Task<VaultOverview?> GetOverviewAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<VaultSearchHit>> SearchAsync(Guid userId, VaultSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// <see langword="null"/> when no note matches <paramref name="note"/>.
    /// </summary>
    Task<VaultNoteDetail?> GetNoteAsync(Guid userId, string note, int maxChars, CancellationToken ct = default);

    /// <summary>
    /// Linked, co-linked and semantically similar notes; <see langword="null"/> when no note matches.
    /// </summary>
    Task<IReadOnlyList<VaultRelatedNote>?> GetRelatedAsync(Guid userId, string note, int topK, CancellationToken ct = default);

    /// <summary>
    /// Semantically similar notes that are not linked to or from the note in either direction; <see langword="null"/>
    /// when no note matches.
    /// </summary>
    Task<IReadOnlyList<VaultRelatedNote>?> SuggestLinksAsync(Guid userId, string note, int topK, CancellationToken ct = default);
}
