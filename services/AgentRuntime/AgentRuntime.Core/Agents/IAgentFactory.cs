using Microsoft.Agents.AI;

namespace AgentRuntime.Core.Agents;

public interface IAgentFactory
{
    AIAgent Create(string agentName, string? overrideInstructions = null, CancellationToken ct = default);
}
