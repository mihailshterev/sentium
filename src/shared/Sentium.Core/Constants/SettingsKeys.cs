namespace Sentium.Core.Constants;

public static class SettingsKeys
{
    public const string Harness = "harness";
    public const string Pdp = "pdp";
    public const string Inference = "inference";
    public const string Vigil = "vigil";
    public const string Mcp = "mcp";

    /// <summary>
    /// Whether self-registration is open (global).
    /// </summary>
    public const string Access = "access";

    /// <summary>
    /// The household's time zone and day-start (global).
    /// </summary>
    public const string Calendar = "calendar";

    /// <summary>
    /// A user's own time zone and day-start, overriding <see cref="Calendar"/> where set.
    /// </summary>
    public const string UserCalendar = "calendar-user";

    /// <summary>
    /// A user's own Companion and voice preferences.
    /// </summary>
    public const string Companion = "companion";

    /// <summary>
    /// A user's own Evaluation preferences, such as the Judge model.
    /// </summary>
    public const string Evaluation = "evaluation";
}
