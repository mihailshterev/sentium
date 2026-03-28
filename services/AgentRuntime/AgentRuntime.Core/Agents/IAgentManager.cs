using AgentRuntime.Core.Dtos;

namespace AgentRuntime.Core.Agents;

public interface IAgentManager
{
    Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);
    Task<AgentResponse> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default);
    Task UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default);
    Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
}
