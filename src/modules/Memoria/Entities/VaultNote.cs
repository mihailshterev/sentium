using Sentium.Core.Common;

namespace Sentium.Memoria.Entities;

internal sealed class VaultNote : IUserOwned, INonAudited
{
    public Guid Id { get; set; }

    public Guid VaultId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Vault-relative path with <c>/</c> separators, including the <c>.md</c> extension, in its original case.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="Path"/> lower-cased and without the extension: the key wikilinks resolve against.
    /// </summary>
    public string PathKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public List<string> Aliases { get; set; } = [];

    /// <summary>
    /// Lower-cased <see cref="Aliases"/>, matched by alias links.
    /// </summary>
    public List<string> AliasKeys { get; set; } = [];

    /// <summary>
    /// Frontmatter scalars and lists as a JSON object, for display; <see langword="null"/> without frontmatter.
    /// </summary>
    public string? FrontmatterJson { get; set; }

    public int WordCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Fingerprint of what the knowledge base holds for this note (content plus path), so an unchanged note is never
    /// re-embedded; <see langword="null"/> until first indexed.
    /// </summary>
    public string? IndexedHash { get; set; }

    public DateTimeOffset? IndexedAt { get; set; }

    public List<NoteLink> OutgoingLinks { get; set; } = [];
}
