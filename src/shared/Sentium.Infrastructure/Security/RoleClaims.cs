using System.Security.Claims;

namespace Sentium.Infrastructure.Security;

public static class RoleClaims
{
    public const string ShortRoleClaimType = "role";

    public static bool IsInRole(ClaimsPrincipal? principal, string role)
    {
        if (principal is null)
        {
            return false;
        }

        return principal.IsInRole(role)
            || principal.FindAll(ShortRoleClaimType).Any(c => c.Value == role)
            || principal.FindAll(ClaimTypes.Role).Any(c => c.Value == role);
    }
}
