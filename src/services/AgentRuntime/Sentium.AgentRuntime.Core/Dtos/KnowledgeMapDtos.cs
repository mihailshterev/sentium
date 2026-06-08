namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record KnowledgeMapNode
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

public sealed record KnowledgeMapResponse
{
    public required IReadOnlyList<KnowledgeMapNode> Nodes { get; init; }
    public int TotalNodes { get; init; }
    public required IReadOnlyList<string> Collections { get; init; }
}

public sealed record KnowledgeMapSearchRequest
{
    public required string Query { get; init; }
    public int TopK { get; init; } = 20;
}

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

public sealed record KnowledgeMapSearchResponse
{
    public required string Query { get; init; }
    public required IReadOnlyList<KnowledgeMapSearchResult> Results { get; init; }
    public int TotalMatches { get; init; }
}
