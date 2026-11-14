using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Proposals;

internal sealed record TimelineProposalDto(
    Guid Id,
    TimelineProposalKind Kind,
    TimelineProposalStatus Status,
    Guid? TaskId,
    string Title,
    string? Description,
    TimelineTaskPriority Priority,
    IReadOnlyList<string> Tags,
    string? AssignedAgent,
    DateOnly? DueDate,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    int Year,
    int Month,
    DateOnly? Date,
    int? FromYear,
    int? FromMonth,
    DateOnly? FromDate,
    string? Rationale,
    string ProposedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
