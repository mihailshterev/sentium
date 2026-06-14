namespace Sentium.Shared.Events;

/// <summary>
/// NATS inter-service contract published on <c>registry.settings.invalidated</c> whenever a settings
/// value changes in the Registry.
/// <para/>
/// This is a <b>semantic</b> event: it identifies <i>which</i> settings changed
/// (<paramref name="Key"/> is a <c>SettingsKeys</c> constant, <paramref name="UserId"/> is the scope
/// - null for global), never a cache-key string. Each receiving service translates this identity to
/// its own private cache key(s) and evicts them (see <c>ISettingsCacheInvalidationHandler</c>).
/// Cache keys are deliberately <b>not</b> part of this cross-service contract, so services can store
/// different value shapes without coupling.
/// </summary>
public sealed record SettingsInvalidatedEvent(string Key, Guid? UserId, DateTimeOffset InvalidatedAt);
