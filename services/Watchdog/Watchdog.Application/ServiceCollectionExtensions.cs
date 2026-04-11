using Microsoft.Extensions.DependencyInjection;
using Watchdog.Core.Metrics;

namespace Watchdog.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWatchdogApplication(this IServiceCollection services)
    {
        services.AddSingleton<IWatchdog, WatchdogService>();
        return services;
    }
}
