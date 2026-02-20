using Microsoft.Extensions.DependencyInjection;
using Sentinel.Application.Engine;

namespace Sentinel.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SentinelPolicyEngine>();
        return services;
    }
}