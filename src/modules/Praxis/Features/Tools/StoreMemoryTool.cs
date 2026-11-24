using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Memories;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Saves a personal fact to the acting user's memories, which they can review, edit and delete on the Memories page.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Memory, Action = ToolAction.Write)]
internal sealed class StoreMemoryTool(
    IUserMemoryService memories,
    IPdpContextAccessor pdpContext,
    ILogger<StoreMemoryTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            StoreAsync,
            ToolNames.StoreMemory,
            "Saves a PERSONAL fact or preference the USER explicitly asked you to remember (their name, a setting, a standing instruction) to long-term memory. " +
            "If the fact changes something you already remember, call recall_memory first and use update_memory on the old memory instead. " +
            "Do NOT use this for things YOU figured out - patterns, conclusions, designs, fixes, or reusable approaches belong in capture_agent_learning instead.");
    }

    public async Task<string> StoreAsync(
        [Description("The exact fact or preference to remember, written as a self-contained sentence.")] string content,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Error: Memory content cannot be empty.";
        }

        // A memory is a personal fact: without an acting user there is no private scope to put it in. Fail closed.
        if (pdpContext.UserId is not { } userId)
        {
            logger.LogWarning("store_memory called without an acting user; refusing to store the memory.");
            return "Error: Memories can only be stored on behalf of a signed-in user, and no user is associated with this run.";
        }

        try
        {
            var result = await memories.RememberAsync(userId, content, ct);

            return result.ReplacedExisting
                ? $"Updated memory {result.Memory.Id}, which said nearly the same thing. I will recall it in future conversations."
                : $"Saved memory {result.Memory.Id}. I will be able to recall it in future conversations.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to store a memory.");
            return $"Error: I encountered a problem while trying to save that memory: {ex.Message}";
        }
    }
}
