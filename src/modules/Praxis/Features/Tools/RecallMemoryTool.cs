using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Memories;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Searches the acting user's memories. Each result carries its id and date, so the agent can tell an outdated memory
/// from a newer one and correct it with <see cref="UpdateMemoryTool"/> or <see cref="ForgetMemoryTool"/>.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.VectorDb, Action = ToolAction.Search)]
internal sealed class RecallMemoryTool(IUserMemoryService memories, IPdpContextAccessor pdpContext) : IAgentTool
{
    private const int MaxResults = 5;

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

        if (pdpContext.UserId is not { } userId)
        {
            return "I have no personal memories in this run: no user is associated with it.";
        }

        var results = await memories.RecallAsync(userId, query, MaxResults, ct);
        if (results.Count == 0)
        {
            return "I searched my memory but couldn't find anything relevant to that.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Recalled Memories] (newer memories win when two disagree; fix outdated ones with update_memory or forget_memory)");
        foreach (var result in results)
        {
            var memory = result.Memory;
            sb.AppendLine(CultureInfo.InvariantCulture, $"- [{memory.Id}] {memory.Content} (saved {memory.UpdatedAt:yyyy-MM-dd})");
        }

        return sb.ToString();
    }
}
