using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Extensions;

public static class PlatformExtensions
{
    /// <summary>
    /// Registers, exactly once, the cross-cutting services every Sentium module relies on: current-user
    /// resolution, the per-user cache and ProblemDetails.
    /// The host must register <c>HybridCache</c>.
    /// </summary>
    public static IHostApplicationBuilder AddSentiumPlatform(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.AddHttpContextAccessor();
        services.AddScoped<SystemScopeContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IScopedCache, ScopedCache>();
        services.AddSentiumProblemDetails();

        return builder;
    }
}
