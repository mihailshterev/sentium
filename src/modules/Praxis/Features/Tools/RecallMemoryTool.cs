using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Tool that allows the agent to specifically search its long-term personal memories.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.VectorDb, Action = ToolAction.Search)]
public sealed class RecallMemoryTool(
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    IPdpContextAccessor pdpContext) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            RecallAsync,
            ToolNames.RecallMemory,
            "Searches your personal long-term memory for facts about the user, preferences, or past project notes. " +
            "Use this when you need to remember something the user told you in a previous session.");
    }

    public async Task<string> RecallAsync(
        [Description("What to search your long-term memory for - a topic, fact, name, or keywords.")] string query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "What should I try to remember?";
        }

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query, ct);

        var results = await vectorRepository.SearchAsync(
            KnowledgeCollections.UserMemories,
            queryEmbedding,
            topK: 3,
            scoreThreshold: 0.7f,
            scope: new KnowledgeScopeFilter(pdpContext.UserId),
            ct: ct);

        if (results.Count == 0)
        {
            return "I searched my memory but couldn't find anything relevant to that.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Recalled Memories]");
        foreach (var res in results)
        {
            sb.AppendLine($"- {res.Chunk.Content} (Stored on: {res.Chunk.CreatedAt:d})");
        }

        return sb.ToString();
    }
}
