using System.Text;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Sentinel;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// Lets an agent explicitly search the shared library of agent learnings - reusable insights,
/// optimizations, and fixes captured from past runs via <c>capture_agent_learning</c>. Relevant
/// learnings are also injected automatically, but this tool allows on-demand, deeper lookups.
/// </summary>
[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.Low,
    RequiresApproval = false)]
public sealed class RecallLearningsTool(
    IAgentLearningService learningService,
    IPdpContextAccessor pdpContext) : IAgentTool
{
    public string Name => "recall_learnings";

    public string Description =>
        "Search the shared library of agent learnings - reusable patterns, optimizations, and fixes captured from past runs. " +
        "Use this before tackling a non-trivial task to reuse a proven approach instead of starting from scratch. " +
        "Input: a plain-text description of the problem or topic.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "What topic should I recall learnings about?";
        }

        var results = await learningService.RecallRelevantAsync(input, pdpContext.UserId, limit: 5, ct: ct);

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
