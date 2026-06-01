using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Agents;

public interface IAgentService
{
    ValueTask<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);
    ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);
    ValueTask<AgentResponse?> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default);
    ValueTask<bool> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default);
    ValueTask<bool> DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
}
