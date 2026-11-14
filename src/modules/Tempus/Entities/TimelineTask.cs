using Sentium.Core.Common;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Entities;

/// <summary>
/// An Entry on the user's year timeline - a Task or a Calendar Event (<see cref="Kind"/>) - planned on a day, or
/// pinned to a month until it gets one.
/// </summary>
internal sealed class TimelineTask : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public TimelineEntryKind Kind { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// The month the task belongs to (always set, also for tasks with a day).
    /// </summary>
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>
    /// The planned day, inside <see cref="Year"/>/<see cref="Month"/>; <see langword="null"/> pins the task to the month.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// The time of day, only with a <see cref="Date"/>. Shown on the chip and used for Reminders.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// The last day of a multi-day Calendar Event, on or after <see cref="Date"/>.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// The Series that created this occurrence; <see langword="null"/> for a one-off Entry.
    /// </summary>
    public Guid? SeriesId { get; set; }

    public TimelineSeries? Series { get; set; }

    /// <summary>
    /// The Series date this occurrence was created for (the first of the month for a month-pinned Series). It never
    /// changes when the occurrence moves, so the Series can recognise it and never creates it twice.
    /// </summary>
    public DateOnly? OccurrenceDate { get; set; }

    /// <summary>
    /// Edited or moved on its own ("this one"), so later Series edits leave it alone.
    /// </summary>
    public bool IsDetached { get; set; }

    /// <summary>
    /// The Reminder; see <see cref="Common.ReminderSchedule"/>. <see langword="null"/> means none.
    /// </summary>
    public int? ReminderOffsetMinutes { get; set; }

    /// <summary>
    /// When the Reminder fires (UTC), computed from the placement and the user's time zone and day-start.
    /// </summary>
    public DateTimeOffset? ReminderDueAt { get; set; }

    /// <summary>
    /// When the Reminder was claimed; it fires once. Cleared when the Entry moves, which re-arms it.
    /// </summary>
    public DateTimeOffset? ReminderSentAt { get; set; }

    /// <summary>
    /// The deadline, which may differ from the planned day.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    public TimelineTaskPriority Priority { get; set; }

    public TimelineTaskStatus Status { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Normalized tags; see <see cref="Sentium.Core.Text.TagNormalizer"/>.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Name of the agent the task is assigned to. Agents live in Praxis and are resolved by name there; Tempus
    /// stores the name only and never validates it.
    /// </summary>
    public string? AssignedAgent { get; set; }

    /// <summary>
    /// Who created the task: the user, or an agent whose proposal the user accepted.
    /// </summary>
    public TimelineAuthorKind Origin { get; set; }

    public string? CreatedByAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TimelineNote> Notes { get; set; } = [];
}
