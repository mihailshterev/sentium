using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Sentium.Infrastructure.Results;
using Sentium.Locus.Common;
using Sentium.Locus.Entities;

namespace Sentium.Locus.Features.Attachments;

/// <summary>
/// Photos, manuals, receipts and other files attached to an asset.
/// </summary>
[ApiController]
[Authorize]
[Route($"{LocusRoutes.Prefix}/assets/{{assetId:guid}}/attachments")]
public sealed class AssetAttachmentsController(IAttachmentService attachmentService) : ControllerBase
{
    /// <summary>
    /// Room for the multipart envelope around a maximum-size file.
    /// </summary>
    private const long MaxRequestBytes = LocusLimits.MaxAttachmentBytes + (1024 * 1024);

    /// <summary>
    /// Lists an asset's attachments.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachments(Guid assetId, CancellationToken ct)
    {
        var attachments = await attachmentService.GetAttachmentsAsync(assetId, ct);
        return attachments is null ? NotFound() : Ok(attachments);
    }

    /// <summary>
    /// Uploads a file (multipart field <c>file</c>, optional <c>kind</c>). The type is detected from the
    /// file's content: images (JPEG, PNG, WebP, GIF, HEIC), PDF and plain text are accepted.
    /// </summary>
    /// <response code="400">Empty, too large or unsupported file.</response>
    /// <response code="409">The asset already has the maximum number of attachments.</response>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadAttachment(Guid assetId, IFormFile? file, [FromForm] AttachmentKind kind = AttachmentKind.Other, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "Choose a file to upload.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid request");
        }

        await using var stream = file.OpenReadStream();
        var result = await attachmentService.UploadAsync(assetId, stream, file.FileName, file.Length, kind, ct);

        return result.IsSuccess ? Created(result.Value!.ContentUrl, result.Value) : result.ToActionResult(this);
    }

    /// <summary>
    /// Streams an attachment. Images are shown inline; every other type is always sent as a download.
    /// </summary>
    [HttpGet("{attachmentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachmentContent(Guid assetId, Guid attachmentId, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        var content = await attachmentService.OpenAsync(assetId, attachmentId, ct);
        if (content is null)
        {
            return NotFound();
        }

        // The file is served from the portal's own origin: never let a browser sniff or run it.
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; img-src 'self'; sandbox";
        Response.Headers[HeaderNames.CacheControl] = "private, max-age=300";

        if (content.IsImage && !download)
        {
            var disposition = new ContentDisposition { Inline = true, FileName = content.FileName };
            Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
            return File(content.Content, content.ContentType, enableRangeProcessing: true);
        }

        return File(content.Content, content.ContentType, content.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid assetId, Guid attachmentId, CancellationToken ct)
        => await attachmentService.DeleteAsync(assetId, attachmentId, ct) ? NoContent() : NotFound();
}
