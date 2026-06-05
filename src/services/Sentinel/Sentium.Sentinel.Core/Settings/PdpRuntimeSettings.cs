namespace Sentium.Sentinel.Core.Settings;

/// <summary>
/// The runtime-configurable Policy Decision Point settings.
/// </summary>
public sealed record PdpRuntimeSettings
{
    public bool LockdownMode { get; init; }
    public int AutonomyLevel { get; init; } = 5;
    public bool SemanticIntentCheckEnabled { get; init; } = true;
    public string IntentCheckModel { get; init; } = string.Empty;
    public int RateLimitMaxRequests { get; init; } = 120;
    public int RateLimitWindowSeconds { get; init; } = 60;
}

/// <summary>
/// Provides cached, Registry-backed access to the runtime PDP settings, and persists updates.
/// </summary>
public interface IPdpRuntimeSettingsProvider
{
    /// <summary>
    /// Returns the current runtime PDP settings.
    /// </summary>
    ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists updated runtime PDP settings to the Registry and evicts the local cache.
    /// </summary>
    Task UpdateAsync(PdpRuntimeSettings settings, CancellationToken ct = default);
}
