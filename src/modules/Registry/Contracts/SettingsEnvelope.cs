using System.Text.Json;

namespace Sentium.Registry.Contracts;

public sealed record SettingsEnvelope(string Key, JsonElement Value, DateTimeOffset UpdatedAt, string? UpdatedBy);
