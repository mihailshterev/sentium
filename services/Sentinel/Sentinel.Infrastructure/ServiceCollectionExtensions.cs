using Sentinel.Core.Policies;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Infrastructure.Policies;
using Sentinel.Infrastructure.Workers;
using Infrastructure.Messaging;

namespace Sentinel.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, NatsEventBus>();
        services.AddSingleton<ISentinelPolicy, BlockOutboundNetworkPolicy>();
        services.AddHostedService<NetworkSentinelWorker>();
        return services;
    }
}
