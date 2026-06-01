using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Agents;

public interface IAgentRepository
{
    Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);
    Task<AgentResponse?> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default);
    Task<AgentResponse?> GetAgentByNameAsync(string name, CancellationToken ct = default);
    Task<bool> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default);
    Task<bool> DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
    Task<int> ResetAgentsModelAsync(string modelName, string defaultModel, CancellationToken ct = default);
}
