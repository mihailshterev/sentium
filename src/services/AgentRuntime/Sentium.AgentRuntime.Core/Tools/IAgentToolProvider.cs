using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Core.Tools;

/// <summary>
/// Resolves the set of AI tools available to a named agent.
/// </summary>
public interface IAgentToolProvider
{
    List<AITool> GetToolsForAgent(string agentName, CancellationToken ct);
}
