using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sentium.Shared.Constants;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// <see cref="ICurrentUser"/> implementation backed by <see cref="IHttpContextAccessor"/>.
/// Claim lookups are deliberately tolerant of the different shapes a token can take
/// (JWT bearer on backend services vs. mapped claims on the cookie/OIDC path).
/// <para>
/// <see cref="IsSystem"/> is driven by <see cref="SystemScopeContext"/>: background workers
/// call <see cref="SystemScopeContext.Activate"/> on their DI scope <em>before</em> resolving
/// any other scoped services so that every dependency in that scope sees <c>IsSystem = true</c>.
/// </para>
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor, SystemScopeContext systemScopeContext) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsSystem => systemScopeContext.IsActive;

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

    public bool IsSovereign => RoleClaims.IsInRole(Principal, SecurityRoles.Sovereign);
}
