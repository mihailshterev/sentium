namespace Sentium.AgentRuntime.Core.Registry;

/// <summary>
/// Typed HTTP client for the Registry service.
/// </summary>
public interface IRegistryClient
{
    /// <summary>
    /// Fetches the current global settings from Registry. Returns null on failure.
    /// </summary>
    Task<SettingsSnapshot?> GetSettingsAsync(CancellationToken ct = default);
}
