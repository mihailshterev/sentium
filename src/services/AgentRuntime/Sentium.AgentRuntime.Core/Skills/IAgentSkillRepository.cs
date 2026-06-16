using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Core.Skills;

/// <summary>
/// Data-access contract for persisted agent skills.
/// </summary>
public interface IAgentSkillRepository
{
    /// <summary>
    /// Returns all stored skills ordered by creation date (bounded by a safety cap). Used by
    /// internal callers that need the full set; the API surface uses <see cref="GetPagedAsync"/>.
    /// </summary>
    Task<IReadOnlyList<AgentSkill>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a page of stored skills (newest first) plus the total count, optionally filtered
    /// to a single <paramref name="skillType"/>.
    /// </summary>
    Task<(IReadOnlyList<AgentSkill> Items, int TotalCount)> GetPagedAsync(AgentSkillType? skillType, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns the skill with the given <paramref name="id"/>, or <see langword="null"/> if not found.
    /// </summary>
    Task<AgentSkill?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the skill with the given <paramref name="name"/>, or <see langword="null"/> if not found.
    /// </summary>
    Task<AgentSkill?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Persists a new skill to the store.
    /// </summary>
    Task AddAsync(AgentSkill skill, CancellationToken ct = default);

    /// <summary>
    /// Applies changes to an existing skill.
    /// </summary>
    Task UpdateAsync(AgentSkill skill, CancellationToken ct = default);

    /// <summary>
    /// Removes the skill with the given <paramref name="id"/> from the store.
    /// Returns <see langword="false"/> when no matching skill exists.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
