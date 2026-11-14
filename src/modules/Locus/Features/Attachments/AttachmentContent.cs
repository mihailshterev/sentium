namespace Sentium.Locus.Features.Attachments;

public sealed record AttachmentContent(Stream Content, string ContentType, string FileName, bool IsImage);
