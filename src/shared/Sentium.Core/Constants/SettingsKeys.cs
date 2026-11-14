namespace Sentium.Core.Constants;

public static class SettingsKeys
{
    public const string Harness = "harness";
    public const string Pdp = "pdp";
    public const string Inference = "inference";
    public const string Vigil = "vigil";
    public const string Mcp = "mcp";

    /// <summary>
    /// The household's time zone and day-start (global).
    /// </summary>
    public const string Calendar = "calendar";

    /// <summary>
    /// A user's own time zone and day-start, overriding <see cref="Calendar"/> where set.
    /// </summary>
    public const string UserCalendar = "calendar-user";
}
