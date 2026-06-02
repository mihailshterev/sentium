using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Core.Skills;

/// <summary>
/// Data-access contract for persisted agent skills.
/// </summary>
public interface IAgentSkillRepository
{
    /// <summary>
    /// Returns all stored skills ordered by creation date.
    /// </summary>
    Task<IReadOnlyList<AgentSkill>> GetAllAsync(CancellationToken ct = default);

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
