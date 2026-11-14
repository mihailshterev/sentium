using Sentium.Core.Common;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Entities;

/// <summary>
/// The rule that produces a repeating Entry's occurrences, plus the template each occurrence is created from.
/// Occurrences are real <see cref="TimelineTask"/> rows, created ahead up to a rolling horizon.
/// </summary>
internal sealed class TimelineSeries : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public TimelineEntryKind Kind { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = [];

    public TimelineTaskPriority Priority { get; set; }

    public string? AssignedAgent { get; set; }

    /// <summary>
    /// Days from an occurrence to its due date; Tasks only.
    /// </summary>
    public int? DueOffsetDays { get; set; }

    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Extra days a multi-day Calendar Event lasts after each occurrence's day.
    /// </summary>
    public int? DurationDays { get; set; }

    public int? ReminderOffsetMinutes { get; set; }

    /// <summary>
    /// The occurrences are pinned to their months (only monthly or yearly); <see cref="StartDate"/> is then the
    /// first of the first month.
    /// </summary>
    public bool IsPinned { get; set; }

    public TimelineRepeatFrequency Frequency { get; set; }

    public int Interval { get; set; }

    public List<DayOfWeek> Weekdays { get; set; } = [];

    /// <summary>
    /// The first occurrence; the rule's rhythm counts from it.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// The last day an occurrence may fall on. Set to the day before a "this and all following" edit or delete.
    /// </summary>
    public DateOnly? Until { get; set; }

    /// <summary>
    /// Occurrence dates deleted on their own, so they are never created again.
    /// </summary>
    public List<DateOnly> ExcludedDates { get; set; } = [];

    /// <summary>
    /// Occurrences exist up to this day.
    /// </summary>
    public DateOnly? MaterializedThrough { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
