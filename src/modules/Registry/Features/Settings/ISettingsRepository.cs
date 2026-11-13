using Sentium.Registry.Entities;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Persistence contract for <see cref="SystemSettings"/> rows.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Returns the settings row for <paramref name="userId"/> (or the global row when
    /// <paramref name="userId"/> is <c>null</c>), or <c>null</c> if it has not been seeded yet.
    /// </summary>
    Task<SystemSettings?> FindAsync(Guid? userId, CancellationToken ct = default);

    Task AddAsync(SystemSettings entity, CancellationToken ct = default);

    Task UpdateAsync(SystemSettings entity, CancellationToken ct = default);
}
