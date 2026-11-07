using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Tool that allows the agent to save a personal fact to the acting user's long-term vector memory, through the
/// ingestion service so it is properly chunked, embedded, and scoped to that user.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Memory, Action = ToolAction.Write)]
public sealed class StoreMemoryTool(
    IDocumentIngestionService ingestionService,
    IPdpContextAccessor pdpContext,
    ILogger<StoreMemoryTool> logger) : IAgentTool
{
    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        StoreAsync,
        ToolNames.StoreMemory,
        "Saves a PERSONAL fact or preference the USER explicitly asked you to remember (their name, a setting, a standing instruction) to long-term memory. " +
        "Do NOT use this for things YOU figured out - patterns, conclusions, designs, fixes, or reusable approaches belong in capture_agent_learning instead.");

    public async Task<string> StoreAsync(
        [Description("The exact fact or preference to remember, written as a self-contained sentence.")] string content,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Error: Memory content cannot be empty.";
        }

        // A memory is a personal fact: without an acting user there is no private scope to put it in, and
        // falling back to the shared knowledge base would expose it to every user. Fail closed.
        if (pdpContext.UserId is not { } userId)
        {
            logger.LogWarning("store_memory called without an acting user; refusing to store the memory.");
            return "Error: Memories can only be stored on behalf of a signed-in user, and no user is associated with this run.";
        }

        try
        {
            var request = new IngestionRequest
            {
                Content = content,
                Source = "Agent_Self_Memory",
                SourceType = IngestionSourceType.Custom,
                Scope = KnowledgeScope.User,
                UserId = userId,
                Metadata = new Dictionary<string, string>
                {
                    { "memory_type", "long_term_recall" },
                    { "importance", "high" }
                }
            };

            await ingestionService.IngestAsync(request, KnowledgeCollections.UserMemories, ct);

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
