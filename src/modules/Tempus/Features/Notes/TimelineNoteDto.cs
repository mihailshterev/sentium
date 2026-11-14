using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Notes;

public sealed record TimelineNoteDto(
    Guid Id,
    int Year,
    int Month,
    DateOnly? Date,
    Guid? TaskId,
    TimelineNoteKind Kind,
    TimelineAuthorKind Author,
    string? AuthorName,
    string Content,
    DateTimeOffset CreatedAt);
