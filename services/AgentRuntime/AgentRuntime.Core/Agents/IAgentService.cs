using AgentRuntime.Core.Dtos;

namespace AgentRuntime.Core.Agents;

public interface IAgentService
{
    ValueTask<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);
    ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);
    ValueTask<AgentResponse> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default);
    ValueTask UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default);
    ValueTask DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
}
