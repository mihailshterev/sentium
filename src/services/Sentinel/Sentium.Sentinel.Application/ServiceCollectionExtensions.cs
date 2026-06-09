using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Application.Engine.Policies;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Application.RateLimiting;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.RateLimiting;

namespace Sentium.Sentinel.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PdpOptions>(configuration.GetSection(PdpOptions.SectionName));

        services.AddScoped<SentinelPolicyEngine>();

        services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();

        // Defense-in-depth policy chain. Order matters: the engine evaluates in registration
        // order and short-circuits on the first deny. Cheap, deterministic, hard-deny policies
        // run first; the expensive LLM-based SemanticIntentPolicy (registered in Infrastructure)
        // runs last. Lockdown is first so the kill-switch halts everything immediately.
        services.AddSingleton<IPdpPolicy, LockdownPolicy>();
        services.AddSingleton<IPdpPolicy, ProtectedResourcePolicy>();
        services.AddSingleton<IPdpPolicy, SensitiveDataEgressPolicy>();
        services.AddSingleton<IPdpPolicy, RateLimitingPolicy>();

        return services;
    }
}
