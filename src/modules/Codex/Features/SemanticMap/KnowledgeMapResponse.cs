namespace Sentium.Codex.Features.SemanticMap;

public sealed record KnowledgeMapResponse
{
    public required IReadOnlyList<KnowledgeMapNode> Nodes { get; init; }
    public int TotalNodes { get; init; }
    public required IReadOnlyList<string> Collections { get; init; }
}
