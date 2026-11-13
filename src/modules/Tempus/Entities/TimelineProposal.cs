using Sentium.Core.Common;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Entities;

/// <summary>
/// A change an agent suggests - a new task, or a new place for an existing one - waiting for the user to accept or
/// reject it. Agents never change tasks directly.
/// </summary>
public sealed class TimelineProposal : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public TimelineProposalKind Kind { get; set; }

    public TimelineProposalStatus Status { get; set; }

    /// <summary>
    /// The task to move (<see cref="TimelineProposalKind.Reschedule"/> only).
    /// </summary>
    public Guid? TaskId { get; set; }

    public TimelineTask? Task { get; set; }

    /// <summary>
    /// The new task's title, or the moved task's title when it was proposed.
    /// </summary>
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public TimelineTaskPriority Priority { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? AssignedAgent { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Target month.
    /// </summary>
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>
    /// Target day; <see langword="null"/> pins the task to the target month.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Where the task was when the move was proposed; a task that has moved since makes the proposal stale.
    /// </summary>
    public int? FromYear { get; set; }

    public int? FromMonth { get; set; }

    public DateOnly? FromDate { get; set; }

    public string? Rationale { get; set; }

    public string ProposedBy { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
