using Sentium.Identity.Contracts;
using Sentium.Registry.Contracts;

namespace Sentium.Identity.Features.Users;

/// <summary>
/// Removes a deleted user's personal settings. Lives in Identity because Registry, which Identity consumes, cannot
/// observe Identity itself.
/// </summary>
internal sealed class UserSettingsRemover(ISettingsService settings) : IUserRemovalObserver
{
    public Task OnUserRemovedAsync(Guid userId, CancellationToken ct) => settings.RemoveUserSettingsAsync(userId, ct);
}
