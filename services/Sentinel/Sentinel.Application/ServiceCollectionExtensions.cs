using Microsoft.Extensions.DependencyInjection;
using Sentinel.Application.Engine;
using Sentinel.Application.Services;
using Sentinel.Core.Stores;

namespace Sentinel.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SentinelPolicyEngine>();
        services.AddSingleton<INetworkEventStore, InMemoryNetworkEventStore>();
        return services;
    }
}