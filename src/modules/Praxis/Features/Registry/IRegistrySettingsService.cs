namespace Sentium.Praxis.Features.Registry;

/// <summary>
/// Resolves the Registry settings Praxis needs as one <see cref="SettingsSnapshot"/>, falling back to
/// <see cref="SettingsSnapshot.Default"/> when they cannot be read. Reads are served from Registry's cache.
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
