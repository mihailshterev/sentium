using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Memories;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Deletes one of the acting user's memories that is wrong or that they asked to forget.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Memory, Action = ToolAction.Write)]
internal sealed class ForgetMemoryTool(IUserMemoryService memories, IPdpContextAccessor pdpContext) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ForgetAsync,
            ToolNames.ForgetMemory,
            "Deletes a personal memory that is wrong or that the user asked you to forget. Use the id shown by recall_memory.");
    }

    public async Task<string> ForgetAsync(
        [Description("The id of the memory to delete, as shown in square brackets by recall_memory.")] string memoryId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(memoryId, out var id))
        {
            return "Error: That is not a memory id. Use the id in square brackets from recall_memory.";
        }

        if (pdpContext.UserId is not { } userId)
        {
            return "Error: Memories can only be changed on behalf of a signed-in user, and no user is associated with this run.";
        }

        return await memories.ForgetAsync(userId, id, ct) ? $"Forgot memory {id}." : $"Error: There is no memory {id}.";
    }
}
