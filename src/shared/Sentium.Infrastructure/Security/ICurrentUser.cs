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

    /// <summary>
    /// <see langword="true"/> when the caller is an internal background/system process (not an HTTP
    /// request on behalf of a user). System callers bypass per-user data scoping in the same
    /// way a Sovereign does. Must be <see langword="false"/> for all anonymous web requests so that
    /// unauthenticated HTTP traffic never gains cross-tenant visibility (fail-closed).
    /// </summary>
    bool IsSystem { get; }
}
