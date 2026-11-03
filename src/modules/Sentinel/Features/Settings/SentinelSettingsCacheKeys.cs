namespace Sentium.Sentinel.Features.Settings;

public static class SentinelSettingsCacheKeys
{
    public static string PdpRuntime(Guid? userId) => $"settings:pdp-runtime:{userId?.ToString() ?? "global"}";
}
