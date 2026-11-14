using Sentium.Core.Common;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Entities;

/// <summary>
/// A task on the user's year timeline: planned on a day, or pinned to a month until it gets one.
/// </summary>
internal sealed class TimelineTask : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// The month the task belongs to (always set, also for tasks with a day).
    /// </summary>
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>
    /// The planned day, inside <see cref="Year"/>/<see cref="Month"/>; <see langword="null"/> pins the task to the month.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// The deadline, which may differ from the planned day.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    public TimelineTaskPriority Priority { get; set; }

    public TimelineTaskStatus Status { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Normalized tags; see <see cref="Sentium.Core.Text.TagNormalizer"/>.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Name of the agent the task is assigned to. Agents live in Praxis and are resolved by name there; Tempus
    /// stores the name only and never validates it.
    /// </summary>
    public string? AssignedAgent { get; set; }

    /// <summary>
    /// Who created the task: the user, or an agent whose proposal the user accepted.
    /// </summary>
    public TimelineAuthorKind Origin { get; set; }

    public string? CreatedByAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TimelineNote> Notes { get; set; } = [];
}
