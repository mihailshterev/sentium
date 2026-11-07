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
    /// Display name of the signed-in user (for "updated by" style audit fields), or <see langword="null"/>
    /// for anonymous and system callers. Never use it for authorization or data scoping - use <see cref="UserId"/>.
    /// </summary>
    string? UserName => null;

    /// <summary>
    /// <see langword="true"/> when the caller is an internal background/system process (not an HTTP
    /// request on behalf of a user). System callers bypass per-user data scoping in the same
    /// way a Sovereign does. Must be <see langword="false"/> for all anonymous web requests so that
    /// unauthenticated HTTP traffic never gains cross-tenant visibility (fail-closed).
    /// </summary>
    bool IsSystem { get; }
}
