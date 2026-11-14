using Sentium.Core.Common;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Entities;

/// <summary>
/// A note, status update or progress report on a task, a day or a month. The month (and day) of a task note follow
/// the task when it moves, so every note can be found by month.
/// </summary>
internal sealed class TimelineNote : IUserOwned
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>
    /// The day the note is on; <see langword="null"/> for a month note.
    /// </summary>
    public DateOnly? Date { get; set; }

    public Guid? TaskId { get; set; }

    public TimelineTask? Task { get; set; }

    public TimelineNoteKind Kind { get; set; }

    public TimelineAuthorKind Author { get; set; }

    /// <summary>
    /// The agent's name for agent notes.
    /// </summary>
    public string? AuthorName { get; set; }

    public string Content { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
