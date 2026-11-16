using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sentium.Infrastructure.Retention;

public static class RetentionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TSweep"/> (scoped, also resolvable as itself) and, once, the worker that runs
    /// every sweep daily.
    /// </summary>
    public static IServiceCollection AddRetentionSweep<TSweep>(this IServiceCollection services) where TSweep : class, IRetentionSweep
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TSweep>();
        services.AddScoped<IRetentionSweep>(sp => sp.GetRequiredService<TSweep>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RetentionWorker>());

        return services;
    }
}
