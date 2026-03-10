using AgentRuntime.Core.Dtos;

namespace AgentRuntime.Core.Agents;

public interface IAgentManager
{
    Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);
}
