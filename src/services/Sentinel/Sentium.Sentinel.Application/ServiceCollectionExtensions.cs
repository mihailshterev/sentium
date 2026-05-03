using Microsoft.Extensions.DependencyInjection;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Application.Services;
using Sentium.Sentinel.Core.Stores;

namespace Sentium.Sentinel.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SentinelPolicyEngine>();
        services.AddSingleton<INetworkEventStore, InMemoryNetworkEventStore>();
        return services;
    }
}
