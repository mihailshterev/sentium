using Sentium.Core.Results;
using Sentium.Infrastructure.Storage;

namespace Sentium.Identity.Features.Avatars;

internal interface IAvatarService
{
    /// <summary>
    /// Replaces the user's picture. <paramref name="content"/> must be seekable. Invalid when the file is empty,
    /// too large or not a JPEG, PNG or WebP image.
    /// </summary>
    Task<Result<AvatarResponse>> UploadAsync(Guid userId, Stream content, long length, CancellationToken ct);

    /// <summary>
    /// Removes the user's picture. Idempotent.
    /// </summary>
    Task DeleteAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Opens the user's picture, or returns <see langword="null"/> when they have none. The caller disposes it.
    /// </summary>
    Task<StoredObject?> OpenAsync(Guid userId, CancellationToken ct);
}
