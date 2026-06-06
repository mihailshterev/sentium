using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Sentium.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizeUserOrSystemAttribute : AuthorizeAttribute
{
    public AuthorizeUserOrSystemAttribute()
    {
        AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{InternalApiKeyDefaults.AuthenticationScheme}";
    }
}
