namespace Sentium.Shared.Constants;

/// <summary>
/// Well-known HybridCache keys shared across service boundaries.
/// The key used by the publisher (Registry) and all consumers must be identical
/// so that L2 (Redis) eviction by the publisher cascades correctly.
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// Per-key, per-scope Registry settings cache key. Used identically by the Registry (publish +
    /// evict) and consumers (cache + NATS eviction). <paramref name="userId"/> is null for
    /// global-scoped settings.
    /// </summary>
    public static string SettingsFor(string key, Guid? userId) => $"settings:{key}:{userId?.ToString() ?? "global"}";
}
