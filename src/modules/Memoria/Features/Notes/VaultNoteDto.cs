using System.Text.Json;
using Sentium.Memoria.Contracts.Models;

namespace Sentium.Memoria.Features.Notes;

internal sealed record VaultNoteDto(
    Guid Id,
    string Path,
    string Title,
    string ObsidianUri,
    string Content,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Aliases,
    JsonElement? Frontmatter,
    int WordCount,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? IndexedAt,
    IReadOnlyList<VaultLinkInfo> Links,
    IReadOnlyList<VaultNoteRef> Backlinks);
