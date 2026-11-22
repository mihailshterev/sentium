using Microsoft.Extensions.Caching.Hybrid;

namespace Sentium.Identity.Features.Security;

internal static class IdentityCacheKeys
{
    /// <summary>
    /// User and role data must go stale sooner than the Sentium.Web host default allows, so Identity passes this
    /// explicitly. The cache is in-process only, so one expiration is all that applies.
    /// </summary>
    internal static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(3)
    };

    internal const string AllUsers = "identity:users:all";
    internal const string UsersTag = "identity:users";

    internal static string UserById(Guid userId) => $"identity:users:{userId}";
    internal static string UserTag(Guid userId) => $"identity:user:{userId}";
    internal static string RolesFor(Guid userId) => $"identity:roles:{userId}";
}
