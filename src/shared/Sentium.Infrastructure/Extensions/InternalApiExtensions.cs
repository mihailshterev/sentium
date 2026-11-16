using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Extensions;

public static class InternalApiExtensions
{
    /// <summary>
    /// Registers the <c>InternalApiKey</c> authentication scheme and the <c>SystemCaller</c>
    /// authorization policy so that endpoints decorated with <see cref="AuthorizeSystemAttribute"/>
    /// accept only internal service-to-service calls authenticated via <c>X-Internal-Token</c>.
    /// </summary>
    public static IHostApplicationBuilder AddInternalApiSecurity(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddInternalApiClient();

        builder.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, InternalApiKeyAuthenticationHandler>(
                InternalApiKeyDefaults.AuthenticationScheme, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.SystemCaller, policy =>
                policy.AddAuthenticationSchemes(InternalApiKeyDefaults.AuthenticationScheme)
                      .RequireAuthenticatedUser());

        return builder;
    }

    /// <summary>
    /// Binds the internal API key for <see cref="InternalApiKeyDelegatingHandler"/> on a host that only calls
    /// internal services and accepts no <c>X-Internal-Token</c> calls itself.
    /// </summary>
    public static IHostApplicationBuilder AddInternalApiClient(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<InternalApiOptions>(
            builder.Configuration.GetSection(InternalApiOptions.SectionName));

        return builder;
    }
}
