using System.Text.Json;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Microsoft.Extensions.Logging;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// Allows agents to persist a discrete insight or conclusion to the long-term
/// knowledge base so that future agent interactions can build upon it.
/// The learning is stored in SQL and immediately embedded into the vector store.
/// </summary>
[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.Low,
    RequiresApproval = false)]
public sealed class CaptureAgentLearningTool(IAgentLearningService learningService, ILogger<CaptureAgentLearningTool> logger) : IAgentTool
{
    public string Name => "capture_agent_learning";

    public string Description =>
        "Captures and stores a discrete learning or insight from this interaction for long-term self-improvement. " +
        "Use this tool at the end of significant analyses to record conclusions, patterns, or lessons. " +
        "Input must be a JSON string: {\"content\": \"The learning text\", \"tags\": \"optional,comma,tags\"}. " +
        "The content should be self-contained, written in markdown, and valuable for future retrieval.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Error: Learning content cannot be empty.";
        }

        try
        {
            var (content, tags) = ParseInput(input);

            if (string.IsNullOrWhiteSpace(content))
            {
                return "Error: Could not extract content from input.";
            }

            // TODO: Add the actual agent name here
            var request = new CaptureAgentLearningRequest(
                AgentName: "Agent",
                Content: content,
                Tags: tags);

            var result = await learningService.CaptureAsync(request, ct);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Agent captured learning {LearningId} with tags: {Tags}", result.Id, tags);
            }

            return $"Learning captured and stored (ID: {result.Id}). It will be available for future agent recall.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to capture agent learning");
            return $"Error: Failed to store learning: {ex.Message}";
        }
    }

    private static (string Content, string Tags) ParseInput(string input)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : input;
            var tags = root.TryGetProperty("tags", out var t) ? t.GetString() ?? string.Empty : string.Empty;

            return (content, tags);
        }
        catch (JsonException)
        {
            return (input, string.Empty);
        }
    }
}
