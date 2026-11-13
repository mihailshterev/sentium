using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Notes;

public sealed record TimelineNoteDto(
    Guid Id,
    int Year,
    int Month,
    DateOnly? Date,
    Guid? TaskId,
    TimelineNoteKind Kind,
    TimelineAuthorKind Author,
    string? AuthorName,
    string Content,
    DateTimeOffset CreatedAt);

/// <summary>
/// A note by the user on a task (<paramref name="TaskId"/>), a day (<paramref name="Date"/>) or a month. A task
/// note is placed where the task is.
/// </summary>
public sealed record CreateTimelineNoteRequest(int Year, int Month, DateOnly? Date, Guid? TaskId, string Content);
