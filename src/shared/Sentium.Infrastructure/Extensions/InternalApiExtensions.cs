using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Extensions;

public static class InternalApiExtensions
{
    /// <summary>
    /// Registers the <c>SystemCaller</c> authorization policy and the supporting handler so
    /// that any endpoint decorated with <see cref="AuthorizeSystemCallerAttribute"/> validates
    /// the <c>X-Internal-Token</c> header.
    /// </summary>
    public static IHostApplicationBuilder AddInternalApiSecurity(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<InternalApiOptions>(
            builder.Configuration.GetSection(InternalApiOptions.SectionName));

        builder.Services.AddSingleton<IAuthorizationHandler, SystemCallerAuthorizationHandler>();

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.SystemCaller, policy =>
                policy.AddRequirements(new SystemCallerRequirement()));

        return builder;
    }
}
