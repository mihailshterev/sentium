using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Locus.Common;
using Sentium.Locus.Data;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;

namespace Sentium.Locus.Features.Attachments;

/// <summary>
/// Attachment lifecycle. Uploads write the blob before the row (a failed insert deletes the blob again);
/// deletes remove the row before the blob. Either way a crash can only leave an unreachable orphan blob,
/// never a row pointing at a missing file.
/// </summary>
public sealed class AttachmentService(
    LocusDbContext db,
    IAttachmentStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    public async Task<IReadOnlyList<AttachmentDto>?> GetAttachmentsAsync(Guid assetId, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct))
        {
            return null;
        }

        var attachments = await db.AssetAttachments
            .AsNoTracking()
            .Where(a => a.AssetId == assetId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return [.. attachments.Select(AssetMapping.ToDto)];
    }

    public async Task<Result<AttachmentDto>> UploadAsync(Guid assetId, Stream content, string fileName, long length, AttachmentKind kind, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanSeek)
        {
            throw new ArgumentException("The attachment stream must be seekable.", nameof(content));
        }

        if (length <= 0)
        {
            return Result<AttachmentDto>.Invalid("The file is empty.");
        }

        if (length > LocusLimits.MaxAttachmentBytes)
        {
            return Result<AttachmentDto>.Invalid($"Files can be at most {LocusLimits.MaxAttachmentBytes / (1024 * 1024)} MB.");
        }

        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct))
        {
            return Result<AttachmentDto>.NotFound();
        }

        if (await db.AssetAttachments.CountAsync(a => a.AssetId == assetId, ct) >= LocusLimits.MaxAttachmentsPerAsset)
        {
            return Result<AttachmentDto>.Conflict($"An asset can have at most {LocusLimits.MaxAttachmentsPerAsset} attachments.");
        }

        var safeName = AttachmentPolicy.SanitizeFileName(fileName);
        var header = new byte[AttachmentPolicy.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        content.Seek(0, SeekOrigin.Begin);

        var contentType = AttachmentPolicy.DetectContentType(header.AsSpan(0, read), safeName);
        if (contentType is null)
        {
            return Result<AttachmentDto>.Invalid("Unsupported file type. Upload an image (JPEG, PNG, WebP, GIF, HEIC), a PDF or a plain-text file.");
        }

        var attachment = new AssetAttachment
        {
            Id = Guid.CreateVersion7(),
            AssetId = assetId,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = length,
            Kind = kind,
            CreatedAt = timeProvider.GetUtcNow(),
            CreatedBy = currentUser.UserName
        };
        attachment.BlobName = $"{assetId}/{attachment.Id}";

        await store.UploadAsync(attachment.BlobName, content, contentType, ct);

        try
        {
            db.AssetAttachments.Add(attachment);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteBlobQuietlyAsync(attachment.BlobName);
            throw;
        }

        return Result<AttachmentDto>.Success(AssetMapping.ToDto(attachment));
    }

    public async Task<AttachmentContent?> OpenAsync(Guid assetId, Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.AssetAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AssetId == assetId, ct);

        if (attachment is null)
        {
            return null;
        }

        var stream = await store.OpenReadAsync(attachment.BlobName, ct);
        if (stream is null)
        {
            logger.LogWarning("Attachment {AttachmentId} has no stored file", attachmentId);
            return null;
        }

        return new AttachmentContent(stream, attachment.ContentType, attachment.FileName, AttachmentPolicy.IsImage(attachment.ContentType));
    }

    public async Task<bool> DeleteAsync(Guid assetId, Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.AssetAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AssetId == assetId, ct);

        if (attachment is null)
        {
            return false;
        }

        await db.AssetAttachments.Where(a => a.Id == attachmentId).ExecuteDeleteAsync(ct);
        await DeleteBlobQuietlyAsync(attachment.BlobName);
        return true;
    }

    private async Task DeleteBlobQuietlyAsync(string blobName)
    {
        try
        {
            await store.DeleteAsync(blobName, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete attachment file {BlobName}", blobName);
        }
    }
}
