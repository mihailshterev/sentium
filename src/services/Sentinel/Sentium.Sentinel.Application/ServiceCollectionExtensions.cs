using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Sentinel.Application.Audit;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Application.RateLimiting;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.RateLimiting;

namespace Sentium.Sentinel.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PdpOptions>(configuration.GetSection(PdpOptions.SectionName));

        services.AddSingleton<SentinelPolicyEngine>();

        services.AddSingleton<IAuditLog, InMemoryAuditLog>();
        services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();

        services.AddSingleton<IPdpPolicy, InvariantGuardPolicy>();
        services.AddSingleton<IPdpPolicy, RateLimitingPolicy>();

        return services;
    }
}
