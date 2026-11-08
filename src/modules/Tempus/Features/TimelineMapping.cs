using Sentium.Core.Results;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Dtos;
using Sentium.Tempus.Entities;

namespace Sentium.Tempus.Features;

internal static class TimelineMapping
{
    public static TimelineTaskDto ToDto(TimelineTask task, int noteCount) => new(
        task.Id,
        task.Title,
        task.Description,
        task.Year,
        task.Month,
        task.Date,
        task.DueDate,
        task.Priority,
        task.Status,
        task.CompletedAt,
        task.Tags,
        task.AssignedAgent,
        task.Origin,
        task.CreatedByAgent,
        noteCount,
        task.CreatedAt,
        task.UpdatedAt);

    public static TimelineNoteDto ToDto(TimelineNote note) => new(
        note.Id,
        note.Year,
        note.Month,
        note.Date,
        note.TaskId,
        note.Kind,
        note.Author,
        note.AuthorName,
        note.Content,
        note.CreatedAt);

    public static TimelineProposalDto ToDto(TimelineProposal proposal) => new(
        proposal.Id,
        proposal.Kind,
        proposal.Status,
        proposal.TaskId,
        proposal.Title,
        proposal.Description,
        proposal.Priority,
        proposal.Tags,
        proposal.AssignedAgent,
        proposal.DueDate,
        proposal.Year,
        proposal.Month,
        proposal.Date,
        proposal.FromYear,
        proposal.FromMonth,
        proposal.FromDate,
        proposal.Rationale,
        proposal.ProposedBy,
        proposal.CreatedAt,
        proposal.ResolvedAt);

    public static TimelineTaskInfo ToInfo(TimelineTask task) => new(
        task.Id,
        task.Title,
        task.Year,
        task.Month,
        task.Date,
        task.DueDate,
        task.Priority,
        task.Status,
        task.Tags,
        task.AssignedAgent);

    public static TimelineNoteInfo ToInfo(TimelineNote note) => new(
        note.Id,
        note.Kind,
        note.Author,
        note.AuthorName,
        note.Content,
        note.CreatedAt);

    /// <summary>
    /// Re-types a non-success result (its status and error carry over).
    /// </summary>
    public static Result<TOut> Propagate<TIn, TOut>(this Result<TIn> result) => result.Status switch
    {
        ResultStatus.NotFound => Result<TOut>.NotFound(result.Error),
        ResultStatus.Conflict => Result<TOut>.Conflict(result.Error ?? "Conflict."),
        ResultStatus.Invalid => Result<TOut>.Invalid(result.Error ?? "Invalid request."),
        _ => throw new InvalidOperationException("Only non-success results can be propagated.")
    };
}
