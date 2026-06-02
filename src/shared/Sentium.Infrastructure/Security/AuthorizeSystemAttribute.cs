using Microsoft.AspNetCore.Authorization;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// Restricts the decorated controller or action to internal service-to-service callers.
/// Equivalent to <c>[Authorize(Policy = Policies.SystemCaller)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizeSystemAttribute : AuthorizeAttribute
{
    public AuthorizeSystemAttribute() : base(Policies.SystemCaller)
    {
        AuthenticationSchemes = InternalApiKeyDefaults.AuthenticationScheme;
    }
}
