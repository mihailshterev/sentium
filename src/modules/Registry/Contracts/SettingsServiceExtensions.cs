using System.Text.Json;

namespace Sentium.Registry.Contracts;

public static class SettingsServiceExtensions
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads the settings for <paramref name="key"/> and deserializes the value into the caller's own
    /// shape, or returns <c>null</c> when the key is unknown.
    /// </summary>
    public static async Task<T?> GetValueAsync<T>(this ISettingsService settings, string key, Guid? userId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var envelope = await settings.GetAsync(key, userId, ct);
        return envelope is null ? default : envelope.Value.Deserialize<T>(WebOptions);
    }
}
