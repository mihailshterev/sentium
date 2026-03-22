using Microsoft.Agents.AI;

namespace AgentRuntime.Core.Agents;

public interface IAgentFactory
{
    Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, CancellationToken ct = default);
}
