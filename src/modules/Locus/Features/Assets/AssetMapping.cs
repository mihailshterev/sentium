using Sentium.Locus.Entities;
using Sentium.Locus.Features.Attachments;

namespace Sentium.Locus.Features.Assets;

public static class AssetMapping
{
    /// <summary>
    /// The outbox entry wins (work is queued or failed); otherwise a stored hash means the knowledge base
    /// is current. A shared asset with neither is reported pending - startup reconciliation will queue it.
    /// </summary>
    public static AssetKnowledgeStatus ResolveKnowledgeStatus(bool isAgentAccessible, string? contentHash, KnowledgeSyncStatus? entryStatus) => entryStatus switch
    {
        KnowledgeSyncStatus.Failed => AssetKnowledgeStatus.Failed,
        KnowledgeSyncStatus.Pending => AssetKnowledgeStatus.Pending,
        _ when !isAgentAccessible => AssetKnowledgeStatus.NotShared,
        _ => contentHash is null ? AssetKnowledgeStatus.Pending : AssetKnowledgeStatus.Synced
    };

    public static string ContentUrl(Guid assetId, Guid attachmentId)
        => $"/{LocusRoutes.Prefix}/assets/{assetId}/attachments/{attachmentId}/content";

    public static AttachmentDto ToDto(AssetAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new AttachmentDto(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.Kind,
            AttachmentPolicy.IsImage(attachment.ContentType),
            ContentUrl(attachment.AssetId, attachment.Id),
            attachment.CreatedAt,
            attachment.CreatedBy);
    }
}
