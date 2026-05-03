using Microsoft.Agents.AI;

namespace Sentium.AgentRuntime.Core.Agents;

public interface IAgentFactory
{
    Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, CancellationToken ct = default);
}
