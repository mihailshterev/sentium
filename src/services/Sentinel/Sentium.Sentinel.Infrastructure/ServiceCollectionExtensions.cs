using Sentium.Sentinel.Core.Policies;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Sentinel.Infrastructure.Policies;
using Sentium.Sentinel.Infrastructure.Workers;
using Sentium.Infrastructure.Messaging;

namespace Sentium.Sentinel.Infrastructure;

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
