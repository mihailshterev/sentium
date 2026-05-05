using Microsoft.Extensions.AI;

namespace Sentium.AgentRuntime.Core.Tools;

public interface IAgentToolProvider
{
    List<AITool> GetToolsForAgent(string agentName, CancellationToken ct);
}
