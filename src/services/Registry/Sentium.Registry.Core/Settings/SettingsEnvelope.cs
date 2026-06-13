using System.Text.Json;

namespace Sentium.Registry.Core.Settings;

public sealed record SettingsEnvelope(string Key, JsonElement Value, DateTimeOffset UpdatedAt, string? UpdatedBy);
