using System.Security.Claims;

namespace Sentium.Infrastructure.Security;

public static class RoleClaims
{
    public static bool IsInRole(ClaimsPrincipal? principal, string role) => principal?.IsInRole(role) == true;
}
