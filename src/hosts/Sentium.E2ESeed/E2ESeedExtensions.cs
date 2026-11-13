using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Testing;

namespace Sentium.E2ESeed;

/// <summary>
/// Registers the e2e seeders. Call only when the host runs in the Testing environment.
/// </summary>
public static class E2ESeedExtensions
{
    public static IServiceCollection AddE2ESeeders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IE2ESeeder, LocusE2EDataSeeder>();
        services.AddSingleton<IE2ESeeder, TempusE2EDataSeeder>();
        services.AddSingleton<IE2ESeeder, PraxisE2EDataSeeder>();

        return services;
    }
}
