namespace Sentium.Infrastructure.Settings;

/// <summary>
/// Translates a settings change (a <c>SettingsKeys</c> identity plus scope) into the
/// eviction of <i>this</i> module's own cache entries. Invoked by <see cref="ISettingsCacheInvalidator"/>.
/// <para/>
/// Each module registers one or more handlers that know how to map a <c>SettingsKeys</c> identity to
/// the private cache key(s) the module stores under. The handler performs the eviction itself so it
/// can pick the right strategy - a single-key <c>RemoveAsync</c>, or a tag-based <c>RemoveByTagAsync</c>
/// when one settings change affects many cached entries (e.g. a global value embedded in per-user
/// snapshots).
/// </summary>
public interface ISettingsCacheInvalidationHandler
{
    /// <summary>
    /// Evicts this service's cache entries for the given settings identity.
    /// </summary>
    /// <param name="settingsKey">The changed settings key (a <c>SettingsKeys</c> constant).</param>
    /// <param name="userId">The affected scope, or null for global settings.</param>
    /// <returns><c>true</c> if this handler owns and evicted that settings identity; otherwise <c>false</c>.</returns>
    ValueTask<bool> TryInvalidateAsync(string settingsKey, Guid? userId, CancellationToken ct);
}
