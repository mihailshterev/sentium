namespace Sentium.Codex.Features.SemanticMap;

public sealed record KnowledgeMapSearchRequest
{
    public required string Query { get; init; }
    public int TopK { get; init; } = 20;
}
