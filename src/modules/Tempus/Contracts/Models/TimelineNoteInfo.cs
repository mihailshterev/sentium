namespace Sentium.Tempus.Contracts.Models;

public sealed record TimelineNoteInfo(
    Guid Id,
    TimelineNoteKind Kind,
    TimelineAuthorKind Author,
    string? AuthorName,
    string Content,
    DateTimeOffset CreatedAt);
