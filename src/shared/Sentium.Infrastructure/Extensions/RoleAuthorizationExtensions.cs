using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Security;
using Sentium.Shared.Constants;

namespace Sentium.Infrastructure.Extensions;

public static class RoleAuthorizationExtensions
{
    /// <summary>
    /// Registers the <see cref="Policies.Sovereign"/> authorization policy so that endpoints
    /// decorated with <see cref="AuthorizeSovereignAttribute"/> accept only Sovereign users.
    /// </summary>
    public static IHostApplicationBuilder AddRoleAuthorization(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Sovereign, policy =>
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                      .RequireAuthenticatedUser()
                      .RequireAssertion(ctx => RoleClaims.IsInRole(ctx.User, SecurityRoles.Sovereign)));

        return builder;
    }
}
