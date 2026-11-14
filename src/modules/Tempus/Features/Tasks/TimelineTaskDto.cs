using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Tasks;

public sealed record TimelineTaskDto(
    Guid Id,
    string Title,
    string? Description,
    int Year,
    int Month,
    DateOnly? Date,
    DateOnly? DueDate,
    TimelineTaskPriority Priority,
    TimelineTaskStatus Status,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    TimelineAuthorKind Origin,
    string? CreatedByAgent,
    int NoteCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
