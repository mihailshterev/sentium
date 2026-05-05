using Microsoft.Extensions.DependencyInjection;
using Sentium.Watchdog.Core.Metrics;

namespace Sentium.Watchdog.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWatchdogApplication(this IServiceCollection services)
    {
        services.AddSingleton<IWatchdog, WatchdogService>();
        return services;
    }
}
