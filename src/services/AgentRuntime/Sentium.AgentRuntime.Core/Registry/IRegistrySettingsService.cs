namespace Sentium.AgentRuntime.Core.Registry;

/// <summary>
/// Provides cached access to Registry global settings.
/// Reads from L1 (in-memory) on the hot path; falls back to Registry HTTP if the cache is empty.
/// </summary>
public interface IRegistrySettingsService
{
    /// <summary>
    /// Returns the current settings, reading from the local in-memory cache or falling back to Registry HTTP.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current settings snapshot.</returns>
    ValueTask<SettingsSnapshot> GetAsync(CancellationToken ct = default);
}
