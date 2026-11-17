namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// A workspace file attached to a user turn. The name is kept so the transcript still shows it after the file is
/// deleted.
/// </summary>
internal sealed record MessageAttachment(Guid FileId, string FileName);
