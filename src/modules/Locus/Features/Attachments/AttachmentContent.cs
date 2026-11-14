namespace Sentium.Locus.Features.Attachments;

internal sealed record AttachmentContent(Stream Content, string ContentType, string FileName, bool IsImage);
