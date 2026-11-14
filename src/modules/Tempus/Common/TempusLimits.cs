namespace Sentium.Tempus.Common;

/// <summary>
/// Field limits shared by the EF model, the request validators, the agent facade and the portal's form schemas.
/// </summary>
internal static class TempusLimits
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public const int TitleMax = 200;
    public const int DescriptionMax = 4000;
    public const int NoteMax = 2000;
    public const int RationaleMax = 1000;
    public const int AgentNameMax = 100;

    public const int TagMax = 32;
    public const int MaxTagsPerTask = 10;

    /// <summary>
    /// Pending agent proposals a user can have at once; further proposals are refused until some are resolved.
    /// </summary>
    public const int MaxPendingProposals = 50;

    /// <summary>
    /// Notes agents may leave per user per (UTC) day.
    /// </summary>
    public const int MaxAgentNotesPerDay = 50;

    public const int MaxUpcomingDays = 42;
    public const int MaxListedTasks = 100;
    public const int MaxTaskNotes = 20;

    public static bool IsValidYear(int year) => year is >= MinYear and <= MaxYear;

    public static bool IsValidMonth(int year, int month) => IsValidYear(year) && month is >= 1 and <= 12;
}
