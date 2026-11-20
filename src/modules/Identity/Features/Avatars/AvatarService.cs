using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Core.Results;
using Sentium.Identity.Data;
using Sentium.Identity.Features.Security;
using Sentium.Infrastructure.Storage;

namespace Sentium.Identity.Features.Avatars;

internal sealed class AvatarService(
    IdentityDbContext dbContext,
    IObjectStore objectStore,
    HybridCache cache,
    TimeProvider timeProvider) : IAvatarService
{
    public const string Bucket = "identity-avatars";

    public async Task<Result<AvatarResponse>> UploadAsync(Guid userId, Stream content, long length, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanSeek)
        {
            throw new ArgumentException("The picture stream must be seekable.", nameof(content));
        }

        if (length <= 0)
        {
            return Result<AvatarResponse>.Invalid("The file is empty.");
        }

        if (length > AvatarPolicy.MaxBytes)
        {
            return Result<AvatarResponse>.Invalid($"Pictures can be at most {AvatarPolicy.MaxBytes / 1024} KB.");
        }

        var header = new byte[AvatarPolicy.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        content.Seek(0, SeekOrigin.Begin);

        var contentType = AvatarPolicy.DetectContentType(header.AsSpan(0, read));
        if (contentType is null)
        {
            return Result<AvatarResponse>.Invalid("Unsupported picture. Upload a JPEG, PNG or WebP image.");
        }

        var key = ObjectKey(userId);
        await objectStore.PutAsync(Bucket, key, content, contentType, ct: ct);

        var updatedAt = timeProvider.GetUtcNow();
        var updated = await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AvatarUpdatedAt, updatedAt), ct);

        if (updated == 0)
        {
            await objectStore.DeleteAsync(Bucket, key, CancellationToken.None);
            return Result<AvatarResponse>.NotFound();
        }

        await ForgetCachedUserAsync(userId, ct);
        return Result<AvatarResponse>.Success(new AvatarResponse(AvatarUrls.For(userId, updatedAt)!));
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AvatarUpdatedAt, (DateTimeOffset?)null), ct);

        await ForgetCachedUserAsync(userId, ct);
        await objectStore.DeleteAsync(Bucket, ObjectKey(userId), ct);
    }

    public Task<StoredObject?> OpenAsync(Guid userId, CancellationToken ct) =>
        objectStore.OpenReadAsync(Bucket, ObjectKey(userId), ct);

    public static string ObjectKey(Guid userId) => userId.ToString();

    private async Task ForgetCachedUserAsync(Guid userId, CancellationToken ct)
    {
        await cache.RemoveByTagAsync(IdentityCacheKeys.UserTag(userId), ct);
        await cache.RemoveAsync(IdentityCacheKeys.AllUsers, ct);
    }
}
