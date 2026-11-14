namespace Sentium.Codex.Features.SemanticMap;

public sealed record KnowledgeMapSearchResponse
{
    public required string Query { get; init; }
    public required IReadOnlyList<KnowledgeMapSearchResult> Results { get; init; }
    public int TotalMatches { get; init; }
}
