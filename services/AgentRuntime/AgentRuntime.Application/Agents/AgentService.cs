using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Dtos;

namespace AgentRuntime.Application.Agents;

public sealed class AgentService(IAgentManager manager) : IAgentService
{
    public Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default)
        => manager.CreateAgentAsync(request, ct);

    public Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default)
        => manager.GetAgentsAsync(ct);
}
