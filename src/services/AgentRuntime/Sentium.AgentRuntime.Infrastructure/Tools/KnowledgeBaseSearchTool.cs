using System.Text;
using System.Text.Json;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// Agent tool that performs a semantic similarity search against the RAG knowledge base
/// and returns formatted snippets with source citations.
/// <para>
/// Registered globally (no <c>AllowedAgents</c> restriction) so every agent can autonomously
/// call this tool when it needs factual context.
/// </para>
/// </summary>
[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.Low,
    RequiresApproval = false)]
public sealed class KnowledgeBaseSearchTool(
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    IOptions<RagOptions> options,
    IPdpContextAccessor pdpContext,
    ILogger<KnowledgeBaseSearchTool> logger) : IAgentTool
{
    private readonly RagOptions ragOptions = options.Value;

    /// <inheritdoc />
    public string Name => "knowledge_base_search";

    /// <inheritdoc />
    public string Description =>
        "Search ALL of your knowledge - the shared knowledge base, your captured learnings, and your saved memories - for relevant context. " +
        "Input should be a JSON object with 'query' (the search string) and optional 'topK' (number of results). " +
        "Example: { \"query\": \"resource lock conflicts\", \"topK\": 5 }";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "No search query provided.";
        }

        var (query, topK) = ParseInput(input);

        if (string.IsNullOrWhiteSpace(query))
        {
            return "Could not extract a search query from the input.";
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Knowledge search - query: '{Query}', topK: {TopK}", query, topK);
        }

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query, ct);
        var scope = new KnowledgeScopeFilter(pdpContext.UserId);

        var collections = ragOptions.SearchCollections is { Length: > 0 } ? ragOptions.SearchCollections : [ragOptions.CollectionName];

        var merged = new List<(string Collection, VectorSearchResult Result)>();
        foreach (var collection in collections)
        {
            try
            {
                var hits = await vectorRepository.SearchAsync(collection, queryEmbedding, topK, ragOptions.ScoreThreshold, scope, ct);
                foreach (var hit in hits)
                {
                    merged.Add((collection, hit));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Knowledge search skipped collection '{Collection}'.", collection);
            }
        }

        if (merged.Count == 0)
        {
            return "No relevant information found in your knowledge for this query.";
        }

        var ranked = merged
            .OrderByDescending(m => m.Result.Score)
            .Take(topK)
            .ToList();

        return FormatResults(ranked);
    }

    private (string query, int topK) ParseInput(string input)
    {
        var trimmed = input.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return (trimmed, ragOptions.DefaultTopK);
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            var query = string.Empty;
            if (root.TryGetProperty("query", out var q)) query = q.GetString() ?? string.Empty;
            else if (root.TryGetProperty("input", out var i)) query = i.GetString() ?? string.Empty;
            else if (root.TryGetProperty("text", out var t)) query = t.GetString() ?? string.Empty;

            var topK = root.TryGetProperty("topK", out var k) && TryReadInt(k, out var n) ? Math.Clamp(n, 1, 20) : ragOptions.DefaultTopK;

            return (query, topK);
        }
        catch (JsonException)
        {
            return (trimmed, ragOptions.DefaultTopK);
        }
    }

    private static bool TryReadInt(JsonElement element, out int value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetInt32(out value);
            case JsonValueKind.String:
                return int.TryParse(element.GetString(), out value);
            default:
                value = 0;
                return false;
        }
    }

    private static string FormatResults(IReadOnlyList<(string Collection, VectorSearchResult Result)> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Knowledge - {results.Count} relevant snippet(s) across your knowledge base, learnings, and memories]");
        sb.AppendLine();

        for (var i = 0; i < results.Count; i++)
        {
            var (collection, r) = results[i];
            sb.AppendLine($"--- Snippet {i + 1} ({StoreLabel(collection)}) ---");
            sb.AppendLine($"Source : {r.Chunk.Source} ({r.Chunk.SourceType})");
            sb.AppendLine($"Score  : {r.Score:P0}");
            sb.AppendLine($"Date   : {r.Chunk.CreatedAt:u}");

            if (r.Chunk.Metadata.Count > 0)
            {
                var meta = string.Join(", ", r.Chunk.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                sb.AppendLine($"Meta   : {meta}");
            }

            sb.AppendLine();
            sb.AppendLine(r.Chunk.Content);
            sb.AppendLine();
        }

        sb.AppendLine("[End of results. Cite sources when using the above content in your answer.]");

        return sb.ToString();
    }

    private static string StoreLabel(string collection) => collection switch
    {
        KnowledgeCollections.KnowledgeBase => "Knowledge Base",
        KnowledgeCollections.AgentLearnings => "Captured Learning",
        KnowledgeCollections.UserMemories => "Saved Memory",
        _ => collection
    };
}
