namespace Sentium.Codex.Features.SemanticMap;

public sealed record KnowledgeMapSearchResult
{
    public required string Id { get; init; }
    public float Score { get; init; }
    public required string Content { get; init; }
    public required string FullContent { get; init; }
    public required string Source { get; init; }
    public required string SourceType { get; init; }
    public required string Collection { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
