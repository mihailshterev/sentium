using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Learnings;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Lets an agent explicitly search the shared library of agent learnings - reusable insights, optimizations, and fixes
/// captured from past runs via <c>capture_agent_learning</c>. Relevant learnings are also injected automatically (see
/// <see cref="LearningsContextProvider"/>), but this tool allows on-demand, deeper lookups.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.VectorDb, Action = ToolAction.Search)]
internal sealed class RecallLearningsTool(
    IAgentLearningService learningService,
    IPdpContextAccessor pdpContext) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            RecallAsync,
            ToolNames.RecallLearnings,
            "Search the shared library of agent learnings - reusable patterns, optimizations, and fixes captured from past runs. " +
            "Use this before tackling a non-trivial task to reuse a proven approach instead of starting from scratch.");
    }

    public async Task<string> RecallAsync(
        [Description("A plain-text description of the problem or topic to find prior learnings about.")] string query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "What topic should I recall learnings about?";
        }

        var results = await learningService.RecallRelevantAsync(query, pdpContext.UserId, limit: 5, ct: ct);

        if (results.Count == 0)
        {
            return "No prior learnings found relevant to that.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Recalled Learnings]");
        foreach (var result in results)
        {
            sb.Append("- ").AppendLine(result.Content.ReplaceLineEndings(" ").Trim());
        }

        return sb.ToString();
    }
}
