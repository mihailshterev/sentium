namespace Sentium.Registry.Core.Settings;

/// <summary>
/// Application-layer contract for reading and writing the global Registry settings.
/// Implementations must cache aggressively because <see cref="GetAsync"/> is called on every
/// agent inference request via the harness pipeline.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Returns the current settings, auto-seeding defaults on first access.
    /// The result is served from the L1 in-memory cache on the hot path.
    /// </summary>
    ValueTask<SettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the updated settings, evicts the shared Redis L2 cache, and publishes a
    /// NATS invalidation event so all consuming service instances evict their local L1 caches.
    /// </summary>
    /// <param name="request">The updated field values to apply.</param>
    /// <param name="updatedBy">Identity name of the caller, recorded for audit.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(UpdateSettingsRequest request, string? updatedBy = null, CancellationToken ct = default);
}
