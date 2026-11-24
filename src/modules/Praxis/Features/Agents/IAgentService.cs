using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Agents;

internal interface IAgentService
{
    /// <summary>
    /// <see cref="ResultStatus.Conflict"/> when the name is a reserved built-in agent name or already used by another
    /// of the current user's agents.
    /// </summary>
    ValueTask<Result<AgentResponse>> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns all agents owned by the current user (bounded by a safety cap). For internal use;
    /// the API uses <see cref="GetAgentsPagedAsync"/>.
    /// </summary>
    ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Newest first.
    /// </summary>
    ValueTask<PagedResponse<AgentResponse>> GetAgentsPagedAsync(int page, int pageSize, CancellationToken ct = default);

    ValueTask<AgentResponse?> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>
    /// <see cref="ResultStatus.Conflict"/> when the new name is reserved or already taken by another of the user's
    /// agents.
    /// </summary>
    ValueTask<Result<AgentResponse>> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default);

    ValueTask<bool> DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
}
