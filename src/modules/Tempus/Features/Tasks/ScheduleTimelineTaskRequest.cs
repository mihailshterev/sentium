namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// Moves a task to a day (<paramref name="Date"/>) or pins it to a month.
/// </summary>
internal sealed record ScheduleTimelineTaskRequest(int Year, int Month, DateOnly? Date);
