namespace Sentium.AgentRuntime.Core.Registry;

/// <summary>
/// Provides cached access to Registry global settings.
/// Reads from L1 (in-memory) on the hot path; falls back to Registry HTTP if the cache is empty.
/// </summary>
public interface IRegistrySettingsService
{
    /// <summary>
    /// Returns the settings
    /// </summary>
    /// <param name="userId">The user whose settings to resolve, or null for global defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current settings snapshot.</returns>
    ValueTask<SettingsSnapshot> GetAsync(Guid? userId, CancellationToken ct = default);
}
