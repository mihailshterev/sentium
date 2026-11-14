namespace Sentium.Codex.Features.SemanticMap;

internal sealed record KnowledgeMapNode
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required string FullContent { get; init; }
    public required string Source { get; init; }
    public required string SourceType { get; init; }
    public required string Collection { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
