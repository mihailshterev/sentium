using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// Creates or replaces a task. <paramref name="Date"/> <see langword="null"/> pins it to
/// <paramref name="Year"/>/<paramref name="Month"/>.
/// </summary>
public sealed record SaveTimelineTaskRequest(
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority Priority = TimelineTaskPriority.Normal,
    TimelineTaskStatus Status = TimelineTaskStatus.Todo,
    IReadOnlyList<string>? Tags = null,
    string? AssignedAgent = null);
