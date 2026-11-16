using Sentium.Registry.Entities;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Persistence contract for <see cref="SystemSettings"/> rows. Writes are optimistic: they report a lost race
/// instead of overwriting a concurrent change.
/// </summary>
internal interface ISettingsRepository
{
    /// <summary>
    /// Returns the settings row for <paramref name="userId"/> (or the global row when
    /// <paramref name="userId"/> is <c>null</c>), or <c>null</c> if it has not been seeded yet.
    /// </summary>
    Task<SystemSettings?> FindAsync(Guid? userId, CancellationToken ct = default);

    /// <returns><see langword="false"/> when another request created the row first.</returns>
    Task<bool> TryAddAsync(SystemSettings entity, CancellationToken ct = default);

    /// <returns><see langword="false"/> when the row changed since <paramref name="entity"/> was read.</returns>
    Task<bool> TryUpdateAsync(SystemSettings entity, CancellationToken ct = default);

    Task RemoveAsync(Guid userId, CancellationToken ct = default);
}
