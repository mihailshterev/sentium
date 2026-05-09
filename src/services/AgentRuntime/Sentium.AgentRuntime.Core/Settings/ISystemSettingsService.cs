namespace Sentium.AgentRuntime.Core.Settings;

/// <summary>
/// Manages the singleton <see cref="Entities.SystemSettings"/> row.
/// Implementations must cache aggressively (short-TTL) because the settings
/// are read on every LLM call via the harness pipeline.
/// </summary>
public interface ISystemSettingsService
{
    /// <summary>
    /// Returns the current settings, creating defaults on first access.
    /// </summary>
    Task<SystemSettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists updated settings and invalidates any caches.
    /// </summary>
    Task UpdateAsync(UpdateSystemSettingsRequest request, string? updatedBy = null, CancellationToken ct = default);
}
