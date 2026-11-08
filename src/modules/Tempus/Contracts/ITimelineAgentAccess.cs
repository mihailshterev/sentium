using Sentium.Core.Results;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Contracts;

/// <summary>
/// The assistants' view of a user's year timeline. Every call names the user explicitly: agent runs (chat
/// approvals, workflows, schedules) carry the acting user on their run context, not on an HTTP request, so the
/// facade never relies on ambient identity. Assistants can read, suggest and annotate - they never change a task
/// directly; tasks and moves they propose wait for the user to accept them.
/// </summary>
public interface ITimelineAgentAccess
{
    /// <param name="year">The year to summarise; <see langword="null"/> for the current one.</param>
    /// <param name="upcomingDays">How many days from today to break down by day.</param>
    Task<TimelineAgentOverview> GetOverviewAsync(Guid userId, int? year = null, int upcomingDays = 14, CancellationToken ct = default);

    Task<IReadOnlyList<TimelineTaskInfo>> ListTasksAsync(Guid userId, TimelineTaskQuery query, CancellationToken ct = default);

    Task<TimelineTaskDetail?> GetTaskAsync(Guid userId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Suggests a new task. Invalid input is <see cref="ResultStatus.Invalid"/>; too many pending proposals is
    /// <see cref="ResultStatus.Conflict"/>.
    /// </summary>
    Task<Result<TimelineProposalReceipt>> ProposeTaskAsync(Guid userId, string agentName, TimelineTaskProposal proposal, CancellationToken ct = default);

    /// <summary>
    /// Suggests moving a task; replaces the agent suggestions still pending for the same task. An unknown task is
    /// <see cref="ResultStatus.NotFound"/>.
    /// </summary>
    Task<Result<TimelineProposalReceipt>> ProposeRescheduleAsync(Guid userId, string agentName, TimelineRescheduleProposal proposal, CancellationToken ct = default);

    /// <summary>
    /// Leaves a note, status update or progress report on a task, day or month.
    /// </summary>
    Task<Result<TimelineNoteReceipt>> AddNoteAsync(Guid userId, string agentName, TimelineNoteInput note, CancellationToken ct = default);
}
