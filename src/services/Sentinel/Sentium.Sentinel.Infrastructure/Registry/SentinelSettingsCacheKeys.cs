namespace Sentium.Sentinel.Infrastructure.Registry;

public static class SentinelSettingsCacheKeys
{
    public static string PdpRuntime(Guid? userId) => $"settings:pdp-runtime:{userId?.ToString() ?? "global"}";
}
