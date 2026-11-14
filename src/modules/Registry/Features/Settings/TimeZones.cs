namespace Sentium.Registry.Features.Settings;

internal static class TimeZones
{
    public static bool IsKnown(string? id) => !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
