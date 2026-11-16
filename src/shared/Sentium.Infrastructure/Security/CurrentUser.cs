using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sentium.Core.Constants;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// <see cref="ICurrentUser"/> implementation backed by <see cref="IHttpContextAccessor"/>. Every scheme (the Identity
/// cookie, Device tokens) puts the user id in <see cref="ClaimTypes.NameIdentifier"/>.
/// <para>
/// Outside a request the identity comes from <see cref="SystemScopeContext"/>: background workers call
/// <see cref="SystemScopeContext.Activate"/> (system) or <see cref="SystemScopeContext.ActAs"/> (one user) on their DI
/// scope <em>before</em> resolving any other scoped services, so every dependency in that scope sees it.
/// </para>
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor, SystemScopeContext systemScopeContext) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => systemScopeContext.ActingUserId is not null || (Principal?.Identity?.IsAuthenticated ?? false);

    public bool IsSystem => systemScopeContext.IsActive;

    public Guid? UserId
    {
        get
        {
            if (systemScopeContext.ActingUserId is { } actingUserId)
            {
                return actingUserId;
            }

            var principal = Principal;
            if (principal is null)
            {
                return null;
            }

            var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsSovereign => systemScopeContext.ActingUserId is null && RoleClaims.IsInRole(Principal, SecurityRoles.Sovereign);

    public string? UserName => systemScopeContext.ActingUserId is null && IsAuthenticated ? Principal?.Identity?.Name : null;
}
