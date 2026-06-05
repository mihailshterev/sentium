using System.Text.Json;

namespace Sentium.Registry.Core.Settings;

/// <summary>
/// Centralized, key-based settings operations. New settings types are added by registering an
/// <see cref="ISettingsDescriptor"/>.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Returns the settings for <paramref name="key"/> scoped to <paramref name="userId"/>
    /// (ignored for global keys), or <c>null</c> if the key is unknown. Served from cache.
    /// </summary>
    Task<SettingsEnvelope?> GetAsync(string key, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Validates and persists the settings for <paramref name="key"/>, evicts the cache, and
    /// publishes a NATS invalidation event. Throws <see cref="KeyNotFoundException"/> for unknown
    /// keys and <see cref="FluentValidation.ValidationException"/> for invalid payloads.
    /// </summary>
    Task<SettingsEnvelope> UpdateAsync(string key, Guid? userId, JsonElement payload, string? updatedBy = null, CancellationToken ct = default);
}
