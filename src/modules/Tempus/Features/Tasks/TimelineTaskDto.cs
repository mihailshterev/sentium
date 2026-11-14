using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Tasks;

/// <param name="Repeat">The Series rule when the Entry is an occurrence.</param>
/// <param name="IsPast">A Calendar Event whose last day is before today in the user's time zone; always false for a Task.</param>
internal sealed record TimelineTaskDto(
    Guid Id,
    TimelineEntryKind Kind,
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    DateOnly? DueDate,
    TimelineTaskPriority Priority,
    TimelineTaskStatus Status,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    TimelineAuthorKind Origin,
    string? CreatedByAgent,
    int NoteCount,
    Guid? SeriesId,
    DateOnly? OccurrenceDate,
    bool IsDetached,
    TimelineRepeat? Repeat,
    int? ReminderOffsetMinutes,
    bool IsPast,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
