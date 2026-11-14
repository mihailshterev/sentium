namespace Sentium.Registry.Features.Settings;

internal static class RegistrySettingsCacheKeys
{
    public static string Envelope(string key, Guid? userId) => $"settings:envelope:{key}:{userId?.ToString() ?? "global"}";
}
