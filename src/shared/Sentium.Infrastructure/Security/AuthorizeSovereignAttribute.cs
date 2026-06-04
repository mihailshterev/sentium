using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Sentium.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizeSovereignAttribute : AuthorizeAttribute
{
    public AuthorizeSovereignAttribute() : base(Policies.Sovereign)
    {
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme;
    }
}
