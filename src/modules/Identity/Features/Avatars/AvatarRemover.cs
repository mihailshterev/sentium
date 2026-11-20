using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Storage;

namespace Sentium.Identity.Features.Avatars;

/// <summary>
/// Deletes a removed user's picture. The account row, and with it the picture's version, is already gone by the
/// time observers run, so only the stored object is left.
/// </summary>
internal sealed class AvatarRemover(IObjectStore objectStore) : IUserRemovalObserver
{
    public Task OnUserRemovedAsync(Guid userId, CancellationToken ct) =>
        objectStore.DeleteAsync(AvatarService.Bucket, AvatarService.ObjectKey(userId), ct);
}
