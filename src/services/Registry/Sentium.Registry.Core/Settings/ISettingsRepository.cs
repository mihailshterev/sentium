using Sentium.Registry.Core.Entities;

namespace Sentium.Registry.Core.Settings;

/// <summary>
/// Persistence contract for the singleton <see cref="SystemSettings"/> row.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Returns the singleton settings row, or <c>null</c> if it has not been seeded yet.
    /// </summary>
    Task<SystemSettings?> FindAsync(CancellationToken ct = default);

    /// <summary>
    /// Inserts a new settings row and immediately persists it.
    /// </summary>
    Task AddAsync(SystemSettings entity, CancellationToken ct = default);

    /// <summary>
    /// Flushes pending change-tracker mutations to the database.
    /// </summary>
    Task SaveAsync(CancellationToken ct = default);
}
