using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Core.Agents;

/// <summary>
/// Persistence-layer access to user-defined agents.
/// </summary>
public interface IAgentRepository
{
    /// <summary>
    /// Inserts a new agent and returns the persisted record.
    /// </summary>
    /// <param name="request">The agent's name, description, and optional model.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns all of the current user's agents (bounded by a safety cap). Used by internal
    /// callers that need the full set; the API surface uses <see cref="GetPagedAsync"/>.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    Task<IReadOnlyList<AgentResponse>> GetAgentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a page of the current user's agents (newest first) plus the total count.
    /// </summary>
    Task<(IReadOnlyList<AgentResponse> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns the agent with the given id, or <see langword="null"/> if it does not exist for the current user.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<AgentResponse?> GetAgentByIdAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>
    /// Returns the agent with the given name (case-insensitive), or <see langword="null"/> if none matches.
    /// </summary>
    /// <param name="name">The agent name to look up.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<AgentResponse?> GetAgentByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Returns all agents owned by <paramref name="userId"/> (bounded by a safety cap), independent of the
    /// ambient data scope. Used by background/system callers (e.g. workflow discovery) that bypass the
    /// per-user query filter but must still restrict results to the user the run acts on behalf of, to
    /// avoid exposing other tenants' agents.
    /// </summary>
    /// <param name="userId">The owning user; a <see langword="null"/> id matches no agents.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<IReadOnlyList<AgentResponse>> GetAgentsForUserAsync(Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the agent with the given name (case-insensitive) owned by <paramref name="userId"/>, or
    /// <see langword="null"/>. Use from background/system callers so name resolution is unambiguous per
    /// user even when the per-user query filter is bypassed.
    /// </summary>
    /// <param name="name">The agent name to look up.</param>
    /// <param name="userId">The owning user; a <see langword="null"/> id matches no agents.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<AgentResponse?> GetAgentByNameForUserAsync(string name, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Determines whether the current user already has an agent with the given name (case-insensitive),
    /// used to enforce name uniqueness before a create or update.
    /// </summary>
    /// <param name="name">The candidate agent name.</param>
    /// <param name="excludeId">An agent id to exclude from the check - pass the agent being updated so a rename to its own name is allowed.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/> if a different agent already uses the name.</returns>
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing agent's name, description, and model.
    /// </summary>
    /// <param name="agentId">The identifier of the agent to update.</param>
    /// <param name="request">The new values.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/> if a matching agent was updated; otherwise <see langword="false"/>.</returns>
    Task<bool> UpdateAgentAsync(Guid agentId, UpdateAgentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the agent with the given id.
    /// </summary>
    /// <param name="agentId">The identifier of the agent to delete.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/> if a matching agent was deleted; otherwise <see langword="false"/>.</returns>
    Task<bool> DeleteAgentAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>
    /// Reassigns every agent currently using <paramref name="modelName"/> to <paramref name="defaultModel"/>,
    /// for example after a model is removed from Ollama.
    /// </summary>
    /// <param name="modelName">The model being retired.</param>
    /// <param name="defaultModel">The model to fall back to.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of agents reassigned.</returns>
    Task<int> ResetAgentsModelAsync(string modelName, string defaultModel, CancellationToken ct = default);
}
