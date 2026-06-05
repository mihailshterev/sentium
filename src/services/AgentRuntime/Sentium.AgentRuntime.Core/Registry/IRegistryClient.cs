namespace Sentium.AgentRuntime.Core.Registry;

/// <summary>
/// Typed HTTP client for the Registry service.
/// </summary>
public interface IRegistryClient
{
    /// <summary>
    /// Fetches the settings from Registry
    /// </summary>
    Task<SettingsSnapshot?> GetSettingsAsync(Guid? userId, CancellationToken ct = default);
}
