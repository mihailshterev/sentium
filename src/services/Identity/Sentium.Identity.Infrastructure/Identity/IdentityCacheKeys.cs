namespace Sentium.Identity.Infrastructure.Identity;

internal static class IdentityCacheKeys
{
    internal const string AllUsers = "identity:users:all";
    internal const string UsersTag = "identity:users";

    internal static string UserById(Guid userId) => $"identity:users:{userId}";
    internal static string UserTag(Guid userId) => $"identity:user:{userId}";
    internal static string RolesFor(Guid userId) => $"identity:roles:{userId}";
    internal static string ClaimsFor(Guid userId, string scopeKey) => $"identity:claims:{userId}:{scopeKey}";
}
