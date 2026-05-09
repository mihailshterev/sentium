namespace Sentium.AgentRuntime.Core.Skills;

/// <summary>
/// Business-logic contract for managing agent skills.
/// </summary>
public interface IAgentSkillService
{
    /// <summary>
    /// Returns all user-defined skills.
    /// </summary>
    Task<IReadOnlyList<AgentSkillDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the skill with the given <paramref name="id"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no skill with the given <paramref name="id"/> exists.</exception>
    Task<AgentSkillDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates and persists a new skill from the supplied <paramref name="request"/>.
    /// </summary>
    Task<AgentSkillDto> CreateAsync(CreateAgentSkillRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates the skill identified by <paramref name="id"/> with values from <paramref name="request"/>.
    /// </summary>
    Task UpdateAsync(Guid id, UpdateAgentSkillRequest request, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes the skill identified by <paramref name="id"/>.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
