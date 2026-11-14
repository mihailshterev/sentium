using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Learnings;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Allows agents to persist a discrete insight or conclusion to the long-term knowledge base so that future agent
/// interactions can build upon it. The learning is stored in the database and immediately embedded into the vector store.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Memory, Action = ToolAction.Write)]
internal sealed class CaptureAgentLearningTool(
    IAgentLearningService learningService,
    IPdpContextAccessor pdpContext,
    ILogger<CaptureAgentLearningTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            CaptureAsync,
            ToolNames.CaptureAgentLearning,
            "Captures and stores a discrete learning or insight from this interaction for long-term self-improvement. " +
            "This is the right place for anything YOU figured out - an architecture blueprint, a debugging fix, a design pattern, or a comparison/conclusion you produced. " +
            "Use this tool at the end of significant analyses to record conclusions, patterns, or lessons. " +
            "Use scope 'Global' ONLY for an abstracted, reusable architectural pattern or execution optimization that any user's agents could benefit from, with NO user-specific details (no names, emails, file paths, hostnames, or personal facts); global requests are validated by the platform and may be kept private if they don't qualify. " +
            "Personal facts and preferences belong in store_memory, not here.");
    }

    public async Task<string> CaptureAsync(
        [Description("The learning text - a self-contained insight, pattern, or fix, written in markdown.")] string content,
        [Description("Optional comma-separated tags for retrieval.")] string? tags = null,
        [Description("'User' (default) or 'Global' for an abstracted, reusable pattern with no user-specific details.")] LearningScope scope = LearningScope.User,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Error: Learning content cannot be empty.";
        }

        try
        {
            var requestGlobal = scope == LearningScope.Global;

            var result = await learningService.CaptureAsync(new CaptureAgentLearningRequest(
                AgentName: AgentToolFunction.CurrentAgentName,
                Content: content,
                Tags: tags ?? string.Empty,
                UserId: pdpContext.UserId,
                RequestGlobal: requestGlobal), ct);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Agent captured learning {LearningId} (global={IsGlobal}) with tags: {Tags}", result.Id, result.IsGlobal, tags);
            }

            if (requestGlobal && result.IsGlobal)
            {
                return $"Learning captured and shared GLOBALLY (ID: {result.Id}). Every user's agents can now build on it.";
            }

            if (requestGlobal)
            {
                return $"Learning saved privately (ID: {result.Id}). It did not qualify for global sharing - global learnings must be abstracted, reusable patterns with no user-specific details.";
            }

            return $"Learning captured and stored (ID: {result.Id}). It will be available for future agent recall.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to capture agent learning");
            return $"Error: Failed to store learning: {ex.Message}";
        }
    }
}
