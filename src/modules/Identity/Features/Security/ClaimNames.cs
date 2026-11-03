namespace Sentium.Identity.Features.Security;

/// <summary>
/// Short, JWT-style claim types emitted alongside the long <see cref="System.Security.Claims.ClaimTypes"/>
/// URIs so callers can read either shape.
/// </summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
}
