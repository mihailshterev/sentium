using Microsoft.Extensions.Caching.Hybrid;

namespace Sentium.Identity.Features.Security;

internal static class IdentityCacheKeys
{
    /// <summary>
    /// Identity's HybridCache entry lifetimes. User, role and claim data must go stale sooner than the
    /// Sentium.Web host defaults allow, so Identity passes these explicitly.
    /// </summary>
    internal static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(3)
    };

    internal const string AllUsers = "identity:users:all";
    internal const string UsersTag = "identity:users";

    internal static string UserById(Guid userId) => $"identity:users:{userId}";
    internal static string UserTag(Guid userId) => $"identity:user:{userId}";
    internal static string RolesFor(Guid userId) => $"identity:roles:{userId}";
    internal static string ClaimsFor(Guid userId, string scopeKey) => $"identity:claims:{userId}:{scopeKey}";
}
