using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Agent tool that performs a semantic similarity search against the RAG knowledge base
/// and returns formatted snippets with source citations.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.VectorDb, Action = ToolAction.Search)]
public sealed class KnowledgeBaseSearchTool(
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    IOptions<RagOptions> options,
    IPdpContextAccessor pdpContext,
    ILogger<KnowledgeBaseSearchTool> logger) : IAgentTool
{
    private readonly RagOptions ragOptions = options.Value;

    private const int MaxTopK = 20;

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            SearchAsync,
            ToolNames.KnowledgeBaseSearch,
            "Search ALL of your knowledge - the shared knowledge base, your captured learnings, and your saved memories - for relevant context.");
    }

    public async Task<string> SearchAsync(
        [Description("The search string describing what you are looking for.")] string query,
        [Description("Maximum number of results to return (1-20).")] int? topK = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "No search query provided.";
        }

        query = query.Trim();
        var limit = topK is { } requested ? Math.Clamp(requested, 1, MaxTopK) : ragOptions.DefaultTopK;

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Knowledge search - query: '{Query}', topK: {TopK}", query, limit);
        }

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query, ct);
        var scope = new KnowledgeScopeFilter(pdpContext.UserId);

        var collections = ragOptions.SearchCollections is { Length: > 0 } ? ragOptions.SearchCollections : [ragOptions.CollectionName];

        var merged = new List<(string Collection, VectorSearchResult Result)>();
        foreach (var collection in collections)
        {
            try
            {
                var hits = await vectorRepository.SearchAsync(collection, queryEmbedding, limit, ragOptions.ScoreThreshold, scope, ct);
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
            .Take(limit)
            .ToList();

        return FormatResults(ranked);
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

    private static string StoreLabel(string collection)
    {
        return collection switch
        {
            KnowledgeCollections.KnowledgeBase => "Knowledge Base",
            KnowledgeCollections.AgentLearnings => "Captured Learning",
            KnowledgeCollections.UserMemories => "Saved Memory",
            _ => collection
        };
    }
}
