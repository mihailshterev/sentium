using Sentium.Core.Common;

namespace Sentium.Memoria.Entities;

/// <summary>
/// One <c>[[wikilink]]</c>, <c>![[embed]]</c> or markdown link to a note. Unresolved links are kept so they resolve
/// as soon as their target note is synced.
/// </summary>
internal sealed class NoteLink : INonAudited
{
    public Guid Id { get; set; }

    public Guid SourceNoteId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// The target as written (without heading or alias).
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// The normalized target the resolver matches: lower-case, <c>/</c> separators, no <c>.md</c>.
    /// </summary>
    public string TargetKey { get; set; } = string.Empty;

    public string? Heading { get; set; }

    public bool IsEmbed { get; set; }

    public Guid? TargetNoteId { get; set; }

    public VaultNote? SourceNote { get; set; }
}
