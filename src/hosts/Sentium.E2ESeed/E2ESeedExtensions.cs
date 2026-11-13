using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Testing;
using Sentium.Locus.Data;
using Sentium.Praxis.Data;
using Sentium.Tempus.Data;

namespace Sentium.E2ESeed;

/// <summary>Registers the e2e seeders. Call only when the host runs in the Testing environment.</summary>
public static class E2ESeedExtensions
{
    public static IServiceCollection AddE2ESeeders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IE2ESeeder, LocusSeeder>();
        services.AddSingleton<IE2ESeeder, TempusSeeder>();
        services.AddSingleton<IE2ESeeder, PraxisSeeder>();

        return services;
    }

    private sealed class LocusSeeder : IE2ESeeder
    {
        public Task SeedAsync(IServiceProvider services, E2ESeedContext context, CancellationToken ct = default) =>
            LocusE2EDataSeeder.SeedAsync(services.GetRequiredService<LocusDbContext>(), context.TimeProvider, ct);
    }

    private sealed class TempusSeeder : IE2ESeeder
    {
        public Task SeedAsync(IServiceProvider services, E2ESeedContext context, CancellationToken ct = default) =>
            TempusE2EDataSeeder.SeedAsync(services.GetRequiredService<TempusDbContext>(), context.UserId, context.TimeProvider, ct);
    }

    private sealed class PraxisSeeder : IE2ESeeder
    {
        public Task SeedAsync(IServiceProvider services, E2ESeedContext context, CancellationToken ct = default) =>
            PraxisE2EDataSeeder.SeedAsync(services.GetRequiredService<PraxisDbContext>(), context.UserId);
    }
}
