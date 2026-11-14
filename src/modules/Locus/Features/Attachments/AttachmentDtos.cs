using Sentium.Locus.Entities;

namespace Sentium.Locus.Features.Attachments;

internal sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    AttachmentKind Kind,
    bool IsImage,
    string ContentUrl,
    DateTimeOffset CreatedAt,
    string? CreatedBy);
