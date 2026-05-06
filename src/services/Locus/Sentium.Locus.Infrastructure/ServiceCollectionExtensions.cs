using Sentium.Locus.Core.Assets;
using Sentium.Locus.Core.Ingestion;
using Sentium.Locus.Core.Locations;
using Sentium.Locus.Infrastructure.Assets;
using Sentium.Locus.Infrastructure.Data;
using Sentium.Locus.Infrastructure.Ingestion;
using Sentium.Locus.Infrastructure.Locations;
using Sentium.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sentium.Locus.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocusInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        services.AddDbContext<LocusDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString(ResourceNames.LocusDb))
        );

        services.AddScoped<ILocationManager, LocationManager>();
        services.AddScoped<IAssetManager, AssetManager>();

        services.AddHttpClient<IAgentIngestionClient, AgentIngestionClient>(client =>
            client.BaseAddress = new Uri($"https+http://{ServiceNames.AgentRuntime}"))
            .AddServiceDiscovery();

        return services;
    }
}
