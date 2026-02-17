using Microsoft.Extensions.AI;

namespace AgentRuntime.Core.Tools;

public interface IAgentToolProvider
{
    List<AITool> GetToolsForAgent(string agentName, CancellationToken ct);
}
