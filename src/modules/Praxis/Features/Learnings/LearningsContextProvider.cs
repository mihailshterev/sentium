using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Sentinel;

namespace Sentium.Praxis.Features.Learnings;

/// <summary>
/// Agent Framework context provider that closes the self-improvement loop: before each agent run it semantically
/// recalls prior learnings relevant to the latest user message and appends them to the agent's instructions.
/// </summary>
/// <remarks>
/// Contributes <see cref="AIContext.Instructions"/> (merged into the single system message) rather than an extra user
/// message, so small local models are not handed back-to-back user turns. Recall is best-effort: failures are handled
/// inside <see cref="IAgentLearningService.RecallRelevantAsync"/> and simply yield no context.
/// </remarks>
public sealed class LearningsContextProvider(IAgentLearningService learningService, IPdpContextAccessor runContext) : AIContextProvider(null, null, null)
{
    private const int MaxLearnings = 4;

    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var latestUserText = context.AIContext.Messages?.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        if (string.IsNullOrWhiteSpace(latestUserText))
        {
            return new AIContext();
        }

        var learnings = await learningService.RecallRelevantAsync(latestUserText, runContext.UserId, MaxLearnings, cancellationToken);

        return learnings.Count == 0 ? new AIContext() : new AIContext { Instructions = BuildBlock(learnings) };
    }

    internal static string BuildBlock(IReadOnlyList<RecalledLearning> learnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### RELEVANT PRIOR LEARNINGS");
        sb.AppendLine("Insights captured from past runs. Consult them before acting and reuse what applies; expand with the `recall_learnings` tool if you need more.");

        foreach (var learning in learnings)
        {
            sb.Append("- ").AppendLine(learning.Content.ReplaceLineEndings(" ").Trim());
        }

        return sb.ToString().TrimEnd();
    }
}
