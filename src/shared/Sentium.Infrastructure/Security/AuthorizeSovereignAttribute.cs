using Microsoft.AspNetCore.Authorization;

namespace Sentium.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizeSovereignAttribute : AuthorizeAttribute
{
    /// <remarks>
    /// Uses the host's default authentication scheme (the portal session cookie in Sentium.Web);
    /// the <see cref="Policies.Sovereign"/> policy is registered by the Identity module.
    /// </remarks>
    public AuthorizeSovereignAttribute() : base(Policies.Sovereign)
    {
    }
}
