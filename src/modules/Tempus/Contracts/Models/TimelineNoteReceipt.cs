namespace Sentium.Tempus.Contracts.Models;

/// <summary>
/// Where a note ended up.
/// </summary>
public sealed record TimelineNoteReceipt(Guid NoteId, int Year, int Month, DateOnly? Date, Guid? TaskId);
