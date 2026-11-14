namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// A note an assistant leaves. It is attached to the task when <paramref name="TaskId"/> is set, else to the day
/// when <paramref name="Date"/> is set, else to <paramref name="Year"/>/<paramref name="Month"/>, else to the
/// current month.
/// </summary>
public sealed record TimelineNoteInput(
    TimelineNoteKind Kind,
    string Content,
    Guid? TaskId = null,
    int? Year = null,
    int? Month = null,
    DateOnly? Date = null);
