using Sentium.Core.Results;
using Sentium.Locus.Entities;

namespace Sentium.Locus.Features.Attachments;

internal interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentDto>?> GetAttachmentsAsync(Guid assetId, CancellationToken ct = default);

    /// <summary>
    /// Stores a file for an asset. <paramref name="content"/> must be seekable (form-file streams are).
    /// </summary>
    Task<Result<AttachmentDto>> UploadAsync(Guid assetId, Stream content, string fileName, long length, AttachmentKind kind, CancellationToken ct = default);

    Task<AttachmentContent?> OpenAsync(Guid assetId, Guid attachmentId, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid assetId, Guid attachmentId, CancellationToken ct = default);
}
