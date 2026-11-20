namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// The shipped templates. They are prompts only; the timeline planner agent has the read-only timeline tools they rely on.
/// </summary>
internal static class ScheduleTemplates
{
    private const string TimelineAgent = "TimelinePlanner";

    public static IReadOnlyList<ScheduleTemplate> All { get; } =
    [
        new(
            "morning-briefing",
            "Morning briefing",
            "Every day at 07:30: today's and tomorrow's entries, overdue tasks and pending proposals.",
            TimelineAgent,
            "0 30 7 ? * *",
            """
            Prepare my morning briefing using the timeline tools.
            1. List today's and tomorrow's entries with their times.
            2. List overdue tasks, oldest first.
            3. List timeline proposals still waiting for my decision.
            4. If you can see open incidents, list them too.
            Keep it short: headings and bullet points, no preamble. If a section is empty, say so in one line.
            """),
        new(
            "weekly-digest",
            "Weekly digest",
            "Sunday at 18:00: what was completed, what slipped and what is coming next week.",
            TimelineAgent,
            "0 0 18 ? * SUN",
            """
            Write my weekly household digest using the timeline tools.
            1. What was completed in the last seven days.
            2. What slipped: tasks that became overdue or were rescheduled this week.
            3. What is coming in the next seven days, by day.
            End with at most three suggestions for the week ahead. Use headings and bullet points.
            """),
    ];
}
