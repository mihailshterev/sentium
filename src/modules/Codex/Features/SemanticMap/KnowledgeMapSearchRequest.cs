namespace Sentium.Codex.Features.SemanticMap;

internal sealed record KnowledgeMapSearchRequest
{
    public required string Query { get; init; }
    public int TopK { get; init; } = 20;
}
