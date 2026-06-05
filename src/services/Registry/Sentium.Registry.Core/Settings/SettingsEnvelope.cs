namespace Sentium.Registry.Core.Settings;

public sealed record SettingsEnvelope(string Key, object Value, DateTimeOffset UpdatedAt, string? UpdatedBy);
