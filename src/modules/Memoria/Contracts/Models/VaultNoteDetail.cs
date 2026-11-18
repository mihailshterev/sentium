namespace Sentium.Memoria.Contracts.Models;

/// <param name="Content">The raw markdown, cut to the requested length when <paramref name="Truncated"/>.</param>
public sealed record VaultNoteDetail(
    VaultNoteRef Note,
    string Content,
    bool Truncated,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<VaultLinkInfo> Links,
    IReadOnlyList<VaultNoteRef> Backlinks,
    DateTimeOffset UpdatedAt);
