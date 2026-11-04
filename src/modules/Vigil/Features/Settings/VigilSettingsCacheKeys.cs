namespace Sentium.Vigil.Features.Settings;

public static class VigilSettingsCacheKeys
{
    public static string Runtime(Guid? userId) => $"settings:vigil-runtime:{userId?.ToString() ?? "global"}";
}
