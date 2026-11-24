using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Memories;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Rewrites one of the acting user's memories when a fact changed, so the old version stops being recalled.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Memory, Action = ToolAction.Write)]
internal sealed class UpdateMemoryTool(
    IUserMemoryService memories,
    IPdpContextAccessor pdpContext,
    ILogger<UpdateMemoryTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            UpdateAsync,
            ToolNames.UpdateMemory,
            "Replaces an outdated personal memory with its corrected version (the user moved, changed a preference, or " +
            "corrected you). Use the id shown by recall_memory.");
    }

    public async Task<string> UpdateAsync(
        [Description("The id of the memory to replace, as shown in square brackets by recall_memory.")] string memoryId,
        [Description("The corrected fact, written as a self-contained sentence.")] string content,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(memoryId, out var id))
        {
            return "Error: That is not a memory id. Use the id in square brackets from recall_memory.";
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return "Error: Memory content cannot be empty. Use forget_memory to remove a memory.";
        }

        if (pdpContext.UserId is not { } userId)
        {
            return "Error: Memories can only be changed on behalf of a signed-in user, and no user is associated with this run.";
        }

        try
        {
            var updated = await memories.UpdateAsync(userId, id, content, ct);
            return updated is null ? $"Error: There is no memory {id}." : $"Updated memory {id}.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to update memory {MemoryId}.", id);
            return $"Error: I could not update that memory: {ex.Message}";
        }
    }
}
