using Sentium.AgentRuntime.Core.Entities;

namespace Sentium.AgentRuntime.Core.Settings;

/// <summary>
/// Defines the contract for managing global system-wide settings.
/// </summary>
public interface ISystemSettingsRepository
{
    /// <summary>
    /// Retrieves the global system settings.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The <see cref="SystemSettings"/> entity if it exists; otherwise, null.</returns>
    Task<SystemSettings?> FindAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new system settings entity and persists it to the data store.
    /// </summary>
    /// <param name="entity">The settings entity to add.</param>
    /// <param name="ct">The cancellation token.</param>
    Task AddAsync(SystemSettings entity, CancellationToken ct = default);

    /// <summary>
    /// Persists any changes made to the tracked system settings entity.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    Task SaveAsync(CancellationToken ct = default);
}
