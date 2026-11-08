namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// A user's tool list for a built-in agent. <paramref name="Tools"/> <see langword="null"/> = every tool.
/// </summary>
public sealed record AgentToolOverrideRecord(string AgentName, IReadOnlyList<string>? Tools);

/// <summary>
/// Persistence for per-user overrides of built-in agents' tool lists. Every lookup takes the owning user explicitly:
/// a Sovereign (or a system-scoped run) bypasses the query filter and must still get its own overrides only.
/// </summary>
public interface IAgentToolOverrideRepository
{
    /// <summary>
    /// The override of <paramref name="agentName"/> (the canonical built-in name) owned by <paramref name="userId"/>, or <see langword="null"/>.
    /// </summary>
    Task<AgentToolOverrideRecord?> GetAsync(string agentName, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Every override owned by <paramref name="userId"/>.
    /// </summary>
    Task<IReadOnlyList<AgentToolOverrideRecord>> GetAllAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Creates or replaces the override of <paramref name="agentName"/>.
    /// </summary>
    Task UpsertAsync(string agentName, Guid userId, IReadOnlyList<string>? tools, CancellationToken ct = default);

    /// <summary>
    /// Removes the override of <paramref name="agentName"/>, restoring the built-in default.
    /// </summary>
    Task DeleteAsync(string agentName, Guid userId, CancellationToken ct = default);
}
