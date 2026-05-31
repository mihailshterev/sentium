using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// <see cref="ICurrentUser"/> implementation backed by <see cref="IHttpContextAccessor"/>.
/// Claim lookups are deliberately tolerant of the different shapes a token can take
/// (JWT bearer on backend services vs. mapped claims on the cookie/OIDC path).
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private const string SovereignRole = "Sovereign";

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var principal = Principal;
            if (principal is null)
            {
                return null;
            }

            var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub")
                ?? principal.FindFirstValue("nameid");

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsSovereign
    {
        get
        {
            var principal = Principal;
            if (principal is null)
            {
                return false;
            }

            return principal.IsInRole(SovereignRole) || principal.FindAll("role").Any(c => c.Value == SovereignRole);
        }
    }
}
