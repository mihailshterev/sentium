namespace Sentium.Infrastructure.Security;

/// <summary>
/// Provides the identity of the user behind the current request so the data layer can
/// scope resources per-user. Resolved from the authenticated <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsSovereign { get; }
}
