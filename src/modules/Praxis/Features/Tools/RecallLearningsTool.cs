using System.Text;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Tools.Attributes;
using Sentium.Praxis.Features.Sentinel;

namespace Sentium.Praxis.Features.Tools;

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
        "Use this before tackling a non-trivial task to reuse a proven approach instead of starting from scratch.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } = [new("query", "A plain-text description of the problem or topic to find prior learnings about.")];

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
