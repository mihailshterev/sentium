namespace Sentium.Tempus.Features.Notes;

/// <summary>
/// A note by the user on a task (<paramref name="TaskId"/>), a day (<paramref name="Date"/>) or a month. A task
/// note is placed where the task is.
/// </summary>
internal sealed record CreateTimelineNoteRequest(int Year, int Month, DateOnly? Date, Guid? TaskId, string Content);
