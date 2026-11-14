namespace Sentium.Tempus.Contracts.Models;

public sealed record TimelineTaskDetail(
    TimelineTaskInfo Task,
    string? Description,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TimelineNoteInfo> Notes,
    int PendingProposals);
