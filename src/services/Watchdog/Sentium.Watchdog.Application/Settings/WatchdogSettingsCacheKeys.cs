namespace Sentium.Watchdog.Application.Settings;

public static class WatchdogSettingsCacheKeys
{
    public static string Runtime(Guid? userId) => $"settings:watchdog-runtime:{userId?.ToString() ?? "global"}";
}
