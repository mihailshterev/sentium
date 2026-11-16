using System.Security.Claims;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Security;

namespace Sentium.Tests.Unit.Modules.Common;

/// <summary>
/// <see cref="ICurrentUser"/> read from a principal the test controls, e.g. a controller's <c>User</c>, so a test that
/// swaps the principal also swaps the current user.
/// </summary>
internal sealed class PrincipalCurrentUser(Func<ClaimsPrincipal?> principal) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(principal()?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool IsAuthenticated => principal()?.Identity?.IsAuthenticated == true;

    public bool IsSovereign => principal()?.IsInRole(SecurityRoles.Sovereign) == true;

    public bool IsSystem => false;
}
