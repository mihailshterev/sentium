using Sentium.Shared.Results;

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
    /// Returns the skill with the given <paramref name="id"/>, or <see langword="null"/> if none exists.
    /// </summary>
    Task<AgentSkillDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates and persists a new skill. Returns a <see cref="ResultStatus.Conflict"/> result when a skill
    /// with the same name already exists.
    /// </summary>
    Task<Result<AgentSkillDto>> CreateAsync(CreateAgentSkillRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates the skill identified by <paramref name="id"/>. Returns <see langword="false"/> when no such skill exists.
    /// </summary>
    Task<bool> UpdateAsync(Guid id, UpdateAgentSkillRequest request, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes the skill identified by <paramref name="id"/>. Returns <see langword="false"/> when no such skill exists.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
