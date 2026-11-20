using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Sentium.Infrastructure.Results;
using Sentium.Infrastructure.Security;

namespace Sentium.Identity.Features.Avatars;

/// <summary>
/// Profile pictures. Users manage only their own; any signed-in user may view anyone's, as household members see
/// each other.
/// </summary>
[ApiController]
[Authorize]
[Route(IdentityRoutes.Prefix)]
internal sealed class AvatarController(IAvatarService avatarService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Room for the multipart envelope around a maximum-size picture.
    /// </summary>
    private const long MaxRequestBytes = AvatarPolicy.MaxBytes + (64 * 1024);

    /// <summary>
    /// Replaces the current user's picture (multipart field <c>file</c>; JPEG, PNG or WebP, at most 1 MB).
    /// </summary>
    /// <response code="400">Empty, too large or unsupported file.</response>
    [HttpPost("account/me/avatar")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType(typeof(AvatarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return Problem(detail: "Choose a picture to upload.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid request");
        }

        await using var stream = file.OpenReadStream();
        var result = await avatarService.UploadAsync(userId.Value, stream, file.Length, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("account/me/avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAvatar(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized();
        }

        await avatarService.DeleteAsync(userId.Value, ct);
        return NoContent();
    }

    /// <summary>
    /// Streams a user's picture. The URL carries a version, so the response may be cached indefinitely.
    /// </summary>
    [HttpGet("users/{id:guid}/avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvatar(Guid id, CancellationToken ct)
    {
        var picture = await avatarService.OpenAsync(id, ct);
        if (picture is null)
        {
            return NotFound();
        }

        // Served from the portal's own origin: never let a browser sniff or run it.
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; sandbox";
        Response.Headers[HeaderNames.CacheControl] = "private, max-age=31536000, immutable";

        return File(picture.Content, picture.ContentType);
    }
}
