using Sentium.Core.Results;

namespace Sentium.Memoria.Features.Notes;

internal sealed record VaultNotesQuery : PaginationQuery
{
    /// <summary>
    /// Matches note titles and paths, case-insensitively.
    /// </summary>
    public string? Search { get; init; }

    public string? Tag { get; init; }

    /// <summary>
    /// Only notes in this folder or below it.
    /// </summary>
    public string? Folder { get; init; }
}
