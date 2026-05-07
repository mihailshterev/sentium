using Sentium.Locus.Core.Assets;
using Sentium.Locus.Core.Ingestion;
using Sentium.Locus.Core.Locations;
using Sentium.Locus.Infrastructure.Assets;
using Sentium.Locus.Infrastructure.Data;
using Sentium.Locus.Infrastructure.Ingestion;
using Sentium.Locus.Infrastructure.Locations;
using Sentium.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Locus.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddLocusInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.AddAuditedDbContext<LocusDbContext>(ResourceNames.LocusDb);

        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddScoped<ILocationManager, LocationManager>();
        services.AddScoped<IAssetManager, AssetManager>();

        services.AddHttpClient<IAgentIngestionClient, AgentIngestionClient>(client =>
            client.BaseAddress = new Uri($"https+http://{ServiceNames.AgentRuntime}"))
            .AddServiceDiscovery();

        return builder;
    }
}
