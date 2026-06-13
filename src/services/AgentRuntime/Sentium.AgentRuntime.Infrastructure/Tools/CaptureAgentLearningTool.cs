using System.Text.Json;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
using Microsoft.Extensions.Logging;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// Allows agents to persist a discrete insight or conclusion to the long-term
/// knowledge base so that future agent interactions can build upon it.
/// The learning is stored in the database and immediately embedded into the vector store.
/// </summary>
[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.Low,
    RequiresApproval = false)]
public sealed class CaptureAgentLearningTool(
    IAgentLearningService learningService,
    IPdpContextAccessor pdpContext,
    ILogger<CaptureAgentLearningTool> logger) : IAgentTool
{
    public string Name => "capture_agent_learning";

    public string Description =>
        "Captures and stores a discrete learning or insight from this interaction for long-term self-improvement. " +
        "This is the right place for anything YOU figured out - an architecture blueprint, a debugging fix, a design pattern, or a comparison/conclusion you produced. " +
        "Use this tool at the end of significant analyses to record conclusions, patterns, or lessons. " +
        "Input must be a JSON string: {\"content\": \"The learning text\", \"tags\": \"optional,comma,tags\", \"scope\": \"user\"}. " +
        "Set \"scope\": \"global\" ONLY for an abstracted, reusable architectural pattern or execution optimization that any user's agents could benefit from, with NO user-specific details (no names, emails, file paths, hostnames, or personal facts). " +
        "Use \"scope\": \"user\" (the default) for anything tied to this user, environment, or task. Personal facts and preferences belong in store_memory, not here. " +
        "Global requests are validated by the platform and may be kept private if they don't qualify. " +
        "The content should be self-contained, written in markdown, and valuable for future retrieval.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Error: Learning content cannot be empty.";
        }

        try
        {
            var (content, tags, requestGlobal) = ParseInput(input);

            if (string.IsNullOrWhiteSpace(content))
            {
                return "Error: Could not extract content from input.";
            }

            var agentName = string.IsNullOrWhiteSpace(pdpContext.AgentName) ? "Agent" : pdpContext.AgentName;

            var request = new CaptureAgentLearningRequest(
                AgentName: agentName,
                Content: content,
                Tags: tags,
                UserId: pdpContext.UserId,
                RequestGlobal: requestGlobal);

            var result = await learningService.CaptureAsync(request, ct);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Agent captured learning {LearningId} (global={IsGlobal}) with tags: {Tags}", result.Id, result.IsGlobal, tags);
            }

            if (requestGlobal && result.IsGlobal)
            {
                return $"Learning captured and shared GLOBALLY (ID: {result.Id}). Every user's agents can now build on it.";
            }

            if (requestGlobal && !result.IsGlobal)
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

    private static (string Content, string Tags, bool RequestGlobal) ParseInput(string input)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : input;
            var tags = root.TryGetProperty("tags", out var t) ? t.GetString() ?? string.Empty : string.Empty;

            var requestGlobal = root.TryGetProperty("scope", out var s) && string.Equals(s.GetString(), "global", StringComparison.OrdinalIgnoreCase);

            return (content, tags, requestGlobal);
        }
        catch (JsonException)
        {
            return (input, string.Empty, false);
        }
    }
}
