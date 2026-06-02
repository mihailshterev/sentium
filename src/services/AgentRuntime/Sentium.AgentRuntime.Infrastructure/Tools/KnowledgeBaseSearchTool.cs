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
/// <para>
/// The LLM may pass either a plain-text query string, or a JSON object with optional <c>topK</c>:
/// <code>{"query": "suspicious outbound traffic", "topK": 3}</code>
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
        "Search the local knowledge base for relevant context. " +
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
            logger.LogInformation("Knowledge base search — query: '{Query}', topK: {TopK}", query, topK);
        }

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query, ct);

        var scope = new KnowledgeScopeFilter(pdpContext.UserId);

        var results = await vectorRepository.SearchAsync(
            ragOptions.CollectionName,
            queryEmbedding,
            topK,
            ragOptions.ScoreThreshold,
            scope,
            ct
        );

        if (results.Count == 0)
        {
            return "No relevant information found in the knowledge base for this query.";
        }

        return FormatResults(results);
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

            var topK = root.TryGetProperty("topK", out var k) && k.TryGetInt32(out var n) ? Math.Clamp(n, 1, 20) : ragOptions.DefaultTopK;

            return (query, topK);
        }
        catch (JsonException)
        {
            return (trimmed, ragOptions.DefaultTopK);
        }
    }

    private static string FormatResults(IReadOnlyList<Core.Rag.Models.VectorSearchResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Knowledge Base — {results.Count} relevant snippet(s)]");
        sb.AppendLine();

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.AppendLine($"--- Snippet {i + 1} ---");
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

        sb.AppendLine("[End of knowledge-base results. Cite sources when using the above content in your answer.]");

        return sb.ToString();
    }
}
