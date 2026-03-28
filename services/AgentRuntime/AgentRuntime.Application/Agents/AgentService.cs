using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;

namespace AgentRuntime.Application.Agents;

public sealed class AgentService(IAgentManager manager) : IAgentService
{
    public Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
        => manager.CreateAgentAsync(request, ct);

    public Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
        => manager.GetAgentsAsync(ct);

    public Task<AgentResponse> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default)
        => manager.GetAgentByIdAsync(agentId, ct);

    public Task UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default)
        => manager.UpdateAgentAsync(agentId, request, ct);

    public Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default)
        => manager.DeleteAgentAsync(agentId, ct);
}
