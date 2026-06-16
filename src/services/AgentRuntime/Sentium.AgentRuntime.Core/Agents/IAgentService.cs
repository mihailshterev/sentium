using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Shared.Results;

namespace Sentium.AgentRuntime.Core.Agents;

/// <summary>
/// Application-level operations for managing user-defined agents.
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Creates a new agent for the current user.
    /// </summary>
    /// <param name="request">The agent's name, description, and optional model.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// <see cref="ResultStatus.Success"/> with the created agent; or <see cref="ResultStatus.Conflict"/> when the name
    /// is a reserved built-in agent name or is already used by another of the current user's agents.
    /// </returns>
    ValueTask<Result<AgentResponse>> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns all agents owned by the current user (bounded by a safety cap). For internal use;
    /// the API uses <see cref="GetAgentsPagedAsync"/>.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    ValueTask<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a page of the current user's agents (newest first).
    /// </summary>
    ValueTask<PagedResponse<AgentResponse>> GetAgentsPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns the agent with the given id, or <see langword="null"/> if it does not exist for the current user.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    ValueTask<AgentResponse?> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing agent's name, description, and model.
    /// </summary>
    /// <param name="agentId">The identifier of the agent to update.</param>
    /// <param name="request">The new values.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// <see cref="ResultStatus.Success"/> with the updated agent; <see cref="ResultStatus.NotFound"/> when no such agent
    /// exists for the current user; or <see cref="ResultStatus.Conflict"/> when the new name is reserved or already taken
    /// by another of the user's agents.
    /// </returns>
    ValueTask<Result<AgentResponse>> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes an agent and its associated data.
    /// </summary>
    /// <param name="agentId">The identifier of the agent to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/> if an agent was deleted; <see langword="false"/> if none existed for the current user.</returns>
    ValueTask<bool> DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
}
