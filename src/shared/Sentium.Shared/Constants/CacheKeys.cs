namespace Sentium.Shared.Constants;

/// <summary>
/// Well-known HybridCache keys shared across service boundaries.
/// The key used by the publisher (Registry) and all consumers must be identical
/// so that L2 (Redis) eviction by the publisher cascades correctly.
/// </summary>
public static class CacheKeys
{
    /// <summary>Global settings managed by the Registry service.</summary>
    public const string Settings = "global:app-settings";
}
