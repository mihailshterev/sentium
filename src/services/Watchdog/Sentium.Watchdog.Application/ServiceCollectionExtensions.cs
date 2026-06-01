using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Messaging;
using Sentium.Shared.Constants;
using Sentium.Watchdog.Application.Monitoring;
using Sentium.Watchdog.Core.Metrics;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWatchdogApplication(this IServiceCollection services)
    {
        services.AddSingleton<IWatchdog, WatchdogService>();

        var channel = Channel.CreateBounded<ServiceHealthStatus>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        services.AddSingleton(channel.Writer);
        services.AddSingleton(channel.Reader);

        services.AddSingleton<IServiceHealthStateStore, ServiceHealthStateStore>();
        services.AddSingleton<IEventBus, NatsEventBus>();

        services.AddHostedService<MonitoringWorker>();
        services.AddHostedService<NatsHealthHub>();

        foreach (var name in new[]
        {
            ServiceNames.Identity,
            ServiceNames.Sentinel,
            ServiceNames.AgentRuntime
        })
        {
#pragma warning disable EXTEXP0001
            services.AddHttpClient(name, client =>
            {
                client.BaseAddress = new Uri($"https+http://{name}");
                client.Timeout = TimeSpan.FromSeconds(5);
            }).AddServiceDiscovery()
            .RemoveAllResilienceHandlers()
            .RemoveAllLoggers();
#pragma warning restore EXTEXP0001
        }

        return services;
    }
}

