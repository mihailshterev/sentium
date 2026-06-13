using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Sentinel;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// Tool that allows the agent to specifically search its long-term personal memories.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, RequiresApproval = false)]
public sealed class RecallMemoryTool(
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    IPdpContextAccessor pdpContext) : IAgentTool
{
    public string Name => "recall_memory";

    public string Description =>
        "Searches your personal long-term memory for facts about the user, preferences, or past project notes. " +
        "Use this when you need to remember something the user told you in a previous session.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "What should I try to remember?";
        }

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(input, ct);

        var results = await vectorRepository.SearchAsync(
            KnowledgeCollections.UserMemories,
            queryEmbedding,
            topK: 3,
            scoreThreshold: 0.7f,
            scope: new KnowledgeScopeFilter(pdpContext.UserId),
            ct: ct
        );

        if (results.Count == 0)
        {
            return "I searched my memory but couldn't find anything relevant to that.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Recalled Memories]");
        foreach (var res in results)
        {
            sb.AppendLine($"- {res.Chunk.Content} (Stored on: {res.Chunk.CreatedAt:d})");
        }

        return sb.ToString();
    }
}
