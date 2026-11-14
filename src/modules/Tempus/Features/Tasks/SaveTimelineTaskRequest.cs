using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// Creates or replaces an Entry. <paramref name="Date"/> <see langword="null"/> pins it to
/// <paramref name="Year"/>/<paramref name="Month"/>. <paramref name="Priority"/>, <paramref name="Status"/>,
/// <paramref name="DueDate"/> and <paramref name="AssignedAgent"/> are Task-only and must be left unset on a Calendar
/// Event; <paramref name="EndDate"/> is Calendar Event-only.
/// </summary>
/// <param name="Kind">A Task when omitted on create. On update it may be omitted; given, it must be the stored kind.</param>
/// <param name="ReminderOffsetMinutes">
/// Minutes before the Entry; for an Entry without a time, 0 (on the day), 1440 (the day before) or 10080 (a week
/// before). <see langword="null"/> sets no Reminder.
/// </param>
internal sealed record SaveTimelineTaskRequest(
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority? Priority = null,
    TimelineTaskStatus? Status = null,
    IReadOnlyList<string>? Tags = null,
    string? AssignedAgent = null,
    TimelineEntryKind? Kind = null,
    TimeOnly? StartTime = null,
    DateOnly? EndDate = null,
    TimelineRepeat? Repeat = null,
    int? ReminderOffsetMinutes = null);
