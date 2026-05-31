using Microsoft.Agents.AI;

namespace Sentium.AgentRuntime.Core.Agents;

public interface IAgentFactory
{
    /// <param name="actingUserId">
    /// The user the agent acts on behalf of. Used to populate the execution context (IPdpContextAccessor)
    /// for scoped tools when there is no ambient HTTP user — e.g. background workflows triggered via NATS.
    /// Pass <c>null</c> for HTTP requests, where the user is already set at the entry point.
    /// </param>
    Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, string? overrideModel = null, Guid? actingUserId = null, CancellationToken ct = default);
}
