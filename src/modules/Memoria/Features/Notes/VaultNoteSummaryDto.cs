namespace Sentium.Memoria.Features.Notes;

/// <param name="Indexed">Whether the knowledge base holds the current version of the note.</param>
internal sealed record VaultNoteSummaryDto(
    Guid Id,
    string Path,
    string Title,
    IReadOnlyList<string> Tags,
    int WordCount,
    DateTimeOffset UpdatedAt,
    bool Indexed);
