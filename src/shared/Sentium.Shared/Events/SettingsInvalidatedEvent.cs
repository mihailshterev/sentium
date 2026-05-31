namespace Sentium.Shared.Events;

/// <summary>
/// NATS inter-service contract published on <c>registry.settings.invalidated</c>.
/// Receiving services must call HybridCache.RemoveAsync(CacheKey) to purge their L1 entry.
/// </summary>
public sealed record SettingsInvalidatedEvent(string CacheKey, DateTimeOffset InvalidatedAt);
