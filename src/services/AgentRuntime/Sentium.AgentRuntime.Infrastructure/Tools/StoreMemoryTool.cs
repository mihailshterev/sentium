using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Core.Tools.Attributes;
using Microsoft.Extensions.Logging;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// Tool that allows the agent to proactively save information to its long-term vector memory.
/// This uses the DocumentIngestionService to ensure the data is properly embedded and stored.
/// </summary>
[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.Low,
    RequiresApproval = false)]
public sealed class StoreMemoryTool(
    IDocumentIngestionService ingestionService,
    ILogger<StoreMemoryTool> logger) : IAgentTool
{
    public string Name => "store_memory";

    public string Description =>
        "Saves important information, user preferences, or project facts to your long-term memory. " +
        "Input should be a descriptive string of the fact to remember in the following format {\"input\": \"Your fact here\"}. " +
        "Use this when the user tells you something they want you to remember for future sessions.";

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Error: Memory content cannot be empty.";
        }

        try
        {
            var request = new IngestionRequest
            {
                Content = input,
                Source = "Agent_Self_Memory",
                SourceType = IngestionSourceType.Custom,
                Metadata = new Dictionary<string, string>
                {
                    { "memory_type", "long_term_recall" },
                    { "importance", "high" }
                }
            };

            await ingestionService.IngestAsync(request, "user_memories", ct);

            logger.LogInformation("Agent successfully stored a new semantic memory.");

            return "Successfully stored that in my long-term memory. I will be able to recall this in future conversations.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store semantic memory.");
            return $"Error: I encountered a problem while trying to save that memory: {ex.Message}";
        }
    }
}
